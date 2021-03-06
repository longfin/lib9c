using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Bencodex.Types;
using Nekoyume.Model.EnumType;
using Nekoyume.TableData;

namespace Nekoyume.Model.Quest
{
    [Serializable]
    public class GoldQuest : Quest
    {
        public readonly TradeType Type;

        public GoldQuest(GoldQuestSheet.Row data, QuestReward reward)
            : base(data, reward)
        {
            Type = data.Type;
        }

        public GoldQuest(Dictionary serialized) : base(serialized)
        {
            Type = (TradeType)(int)((Integer)serialized["type"]).Value;
        }

        public override QuestType QuestType => QuestType.Exchange;

        public override void Check()
        {
            if (Complete)
                return;

            Complete = _current >= Goal;
        }

        // FIXME: 이 메서드 구현은 중복된 코드가 다른 데서도 많이 있는 듯.
        public override string GetProgressText() =>
            string.Format(
                CultureInfo.InvariantCulture,
                GoalFormat,
                Math.Min(Goal, _current),
                Goal
            );

        protected override string TypeId => "GoldQuest";

        public void Update(BigInteger gold)
        {
            if (Complete)
                return;

            // FIXME: _current를 BigInteger로 바꾸는 게 좋지 않을까요…
            // 이대로라면 overflow로 돈을 2^32 NCG 이상 벌면 같은 퀘스트 두 번 이상 깰 수 있을 듯.
            _current += (int)gold;
            Check();
        }

        public override IValue Serialize() =>
            new Dictionary(new Dictionary<IKey, IValue>
            {
                [(Text)"type"] = (Integer)(int)Type,
            }.Union((Dictionary)base.Serialize()));

    }
}
