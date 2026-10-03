using System;
using MinikDuello.ParentControls;
using NUnit.Framework;

namespace MinikDuello.Tests
{
    public sealed class ParentGateSessionTests
    {
        private const int Seed = 42;
        private const double Now = 100d;

        private static ParentGateSession Create() => new ParentGateSession(new Random(Seed));

        [Test]
        public void Question_UsesConfiguredRanges()
        {
            for (int i = 0; i < 100; i++)
            {
                var session = new ParentGateSession(new Random(i));
                Assert.That(session.Left, Is.InRange(ParentGateRules.MinLeft, ParentGateRules.MaxLeft));
                Assert.That(session.Right, Is.InRange(ParentGateRules.MinRight, ParentGateRules.MaxRight));
            }
        }

        [Test]
        public void CorrectAnswer_Passes()
        {
            ParentGateSession session = Create();
            Assert.AreEqual(GateResult.Passed, session.Submit(session.Left + session.Right, Now));
        }

        [Test]
        public void WrongAnswer_ReturnsWrong_AndAsksNewQuestion()
        {
            ParentGateSession session = Create();
            int wrong = session.Left + session.Right + 1;
            Assert.AreEqual(GateResult.Wrong, session.Submit(wrong, Now));
        }

        [Test]
        public void RepeatedWrongAnswers_LockOut_ThenUnlockAfterTimeout()
        {
            ParentGateSession session = Create();
            GateResult last = GateResult.Wrong;
            for (int i = 0; i < ParentGateRules.MaxWrongAttempts; i++)
            {
                last = session.Submit(-1, Now);
            }

            Assert.AreEqual(GateResult.LockedOut, last);
            Assert.IsTrue(session.IsLockedOut(Now));
            // Kilitliyken doğru cevap bile geçmez.
            Assert.AreEqual(GateResult.LockedOut, session.Submit(session.Left + session.Right, Now));

            double later = Now + ParentGateRules.LockoutSeconds + 1d;
            Assert.IsFalse(session.IsLockedOut(later));
            Assert.AreEqual(GateResult.Passed, session.Submit(session.Left + session.Right, later));
        }
    }
}
