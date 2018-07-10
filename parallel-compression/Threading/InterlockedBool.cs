using System.Threading;

namespace Parallel.Compression.Threading
{
    public class InterlockedBool
    {
        private const int True = 1;
        private const int False = 0;

        private int state;

        public InterlockedBool(bool initialValue)
        {
            state = initialValue ? True : False;
        }

        public void Set(bool value)
        {
            Interlocked.Exchange(ref state, value ? True : False);
        }

        public bool TrySwitchTo(bool value)
        {
            var newState = value ? True : False;
            var expectedState = value ? True : False;
            return Interlocked.CompareExchange(ref state, newState, expectedState) == expectedState;
        }

        public static implicit operator bool(InterlockedBool interlockedBool)
        {
            return interlockedBool.state == True;
        }

        public override string ToString()
        {
            return ((bool) this).ToString();
        }
    }
}