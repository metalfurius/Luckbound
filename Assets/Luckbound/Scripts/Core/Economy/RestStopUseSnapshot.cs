using System;

namespace Luckbound.Core.Economy
{
    /// <summary>Persisted once-only recovery and Talia use for one room instance.</summary>
    [Serializable]
    public sealed class RestStopUseSnapshot
    {
        public string roomInstanceId;
        public bool entered;
        public int entryHealthRestored;
        public bool taliaUsed;
        public int taliaHealthRestored;
        public int taliaHealthAfter;

        public RestStopUseSnapshot Clone()
        {
            return new RestStopUseSnapshot
            {
                roomInstanceId = roomInstanceId,
                entered = entered,
                entryHealthRestored = entryHealthRestored,
                taliaUsed = taliaUsed,
                taliaHealthRestored = taliaHealthRestored,
                taliaHealthAfter = taliaHealthAfter
            };
        }
    }
}
