using MinikDuello.Domain.Net;
using NUnit.Framework;

namespace MinikDuello.Tests
{
    public sealed class FriendCodeTests
    {
        [Test]
        public void Format_AndValidation_MatchServerRules()
        {
            Assert.IsTrue(FriendCodes.IsValid(FriendCodes.Format("PANDA", "4832")));
            Assert.IsFalse(FriendCodes.IsValid("PANDA-0832")); // 0 ve 1 yok
            Assert.IsFalse(FriendCodes.IsValid("PANDA-483"));
            Assert.IsFalse(FriendCodes.IsValid("HACKER-4832"));
            Assert.IsFalse(FriendCodes.IsValid("panda-4832"));
            Assert.IsFalse(FriendCodes.IsValid(null));
            Assert.IsFalse(FriendCodes.IsValid("PANDA4832"));
        }

        [Test]
        public void Preview_ShowsPlaceholders()
        {
            Assert.AreEqual("PANDA-48__", FriendCodes.Preview("PANDA", "48"));
            Assert.AreEqual("_____-____", FriendCodes.Preview(null, null));
        }

        [Test]
        public void Animals_AreEightAndNamed()
        {
            Assert.AreEqual(8, FriendCodes.AnimalKeys.Length);
            Assert.AreEqual(FriendCodes.AnimalKeys.Length, FriendCodes.AnimalNames.Length);
        }
    }
}
