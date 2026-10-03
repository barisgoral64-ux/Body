using MinikDuello.UI;
using NUnit.Framework;

namespace MinikDuello.Tests
{
    public sealed class ScreenStackTests
    {
        private ScreenStack<ScreenId> stack;

        [SetUp]
        public void SetUp()
        {
            stack = new ScreenStack<ScreenId>();
            stack.Reset(ScreenId.MainMenu);
        }

        [Test]
        public void Push_ThenPop_ReturnsToPrevious()
        {
            stack.Push(ScreenId.PlayMenu);
            Assert.AreEqual(ScreenId.PlayMenu, stack.Current);

            Assert.IsTrue(stack.Pop(out ScreenId previous));
            Assert.AreEqual(ScreenId.MainMenu, previous);
        }

        [Test]
        public void Pop_AtRoot_DoesNothing()
        {
            Assert.IsFalse(stack.Pop(out _));
            Assert.AreEqual(ScreenId.MainMenu, stack.Current);
            Assert.AreEqual(1, stack.Count);
        }

        [Test]
        public void Push_SameScreenTwice_IsIgnored()
        {
            stack.Push(ScreenId.PlayMenu);
            stack.Push(ScreenId.PlayMenu);
            Assert.AreEqual(2, stack.Count);
        }

        [Test]
        public void ReplaceTop_SwapsCurrentWithoutGrowing()
        {
            stack.Push(ScreenId.ParentGate);
            stack.ReplaceTop(ScreenId.ParentDashboard);

            Assert.AreEqual(ScreenId.ParentDashboard, stack.Current);
            Assert.AreEqual(2, stack.Count);
            stack.Pop(out ScreenId previous);
            Assert.AreEqual(ScreenId.MainMenu, previous);
        }
    }
}
