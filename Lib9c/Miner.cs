using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Tx;
using Nekoyume.Action;
using Serilog;

namespace Nekoyume.BlockChain
{
    public class Miner
    {
        private readonly BlockChain<PolymorphicAction<ActionBase>> _chain;
        private readonly Swarm<PolymorphicAction<ActionBase>> _swarm;
        private readonly PrivateKey _privateKey;

        public bool AuthorizedMiner { get; }

        public Address Address => _privateKey.ToAddress();

        public Transaction<PolymorphicAction<ActionBase>> StageProofTransaction()
        {
            // We assume authorized miners create no transactions at all except for
            // proof transactions.  Without the assumption, nonces for proof txs become
            // much complicated to determine.
            var proof = Transaction<PolymorphicAction<ActionBase>>.Create(
                _chain.GetNextTxNonce(_privateKey.ToAddress()),
                _privateKey,
                _chain.Genesis.Hash,
                new PolymorphicAction<ActionBase>[0]
            );
            _chain.StageTransaction(proof);
            return proof;
        }

        public async Task<Block<PolymorphicAction<ActionBase>>> MineBlockAsync(
            int maxTransactions,
            CancellationToken cancellationToken)
        {
            var txs = new HashSet<Transaction<PolymorphicAction<ActionBase>>>();
            var invalidTxs = txs;

            Block<PolymorphicAction<ActionBase>> block = null;
            try
            {
                if (AuthorizedMiner)
                {
                    Transaction<PolymorphicAction<ActionBase>> authProof = StageProofTransaction();
                    Block<PolymorphicAction<ActionBase>> tip = _chain.Tip;
                    block = Block<PolymorphicAction<ActionBase>>.Mine(
                        tip.Index + 1,
                        _chain.Policy.GetNextBlockDifficulty(_chain),
                        tip.TotalDifficulty,
                        Address,
                        tip.Hash,
                        DateTimeOffset.UtcNow,
                        new[] { authProof },
                        Block<PolymorphicAction<ActionBase>>.CurrentProtocolVersion,
                        cancellationToken
                    );
                    _chain.Append(block);
                }
                else
                {
                    block = await _chain.MineBlock(
                        Address,
                        DateTimeOffset.UtcNow,
                        cancellationToken: cancellationToken,
                        maxTransactions: maxTransactions);
                }

                if (_swarm is Swarm<PolymorphicAction<ActionBase>> s && s.Running)
                {
                    s.BroadcastBlock(block);
                }
            }
            catch (OperationCanceledException)
            {
                Log.Debug("Mining was canceled due to change of tip.");
            }
            catch (InvalidTxException invalidTxException)
            {
                var invalidTx = _chain.GetTransaction(invalidTxException.TxId);

                Log.Debug(
                    invalidTxException, 
                    "Tx[{TxId}] is invalid. mark to unstage. [exc: {@exc}]", 
                    invalidTxException.TxId, 
                    invalidTxException
                );
                invalidTxs.Add(invalidTx);
            }
            catch (UnexpectedlyTerminatedActionException actionException)
            {
                if (actionException.TxId is TxId txId)
                {
                    Log.Debug(
                        $"Tx[{actionException.TxId}]'s action is invalid. mark to unstage. {actionException}");
                    invalidTxs.Add(_chain.GetTransaction(txId));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"exception was thrown. {ex}");
            }
            finally
            {
#pragma warning disable LAA1002
                foreach (var invalidTx in invalidTxs)
#pragma warning restore LAA1002
                {
                    _chain.UnstageTransaction(invalidTx);
                }

            }

            return block;
        }

        public Miner(
            BlockChain<PolymorphicAction<ActionBase>> chain,
            Swarm<PolymorphicAction<ActionBase>> swarm,
            PrivateKey privateKey,
            bool authorizedMiner
        )
        {
            _chain = chain ?? throw new ArgumentNullException(nameof(chain));
            _swarm = swarm;
            _privateKey = privateKey;
            AuthorizedMiner = authorizedMiner;
        }
    }
}
