using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class LabModeGuardTests
{
    [TestMethod]
    public void CanMutate_FailsClosed_WhenMutationIsDisabled()
    {
        var guard = new LabModeGuard(false, true, new[] { "authorized" });

        Assert.IsFalse(guard.CanMutate(true, null));
        Assert.IsFalse(guard.CanMutate(false, "authorized"));
    }

    [TestMethod]
    public void CanMutate_AllowsOnlySinglePlayerOrWhitelistedLobby()
    {
        var guard = new LabModeGuard(true, true, new[] { "authorized" });

        Assert.IsTrue(guard.CanMutate(true, null));
        Assert.IsTrue(guard.CanMutate(false, "authorized"));
        Assert.IsFalse(guard.CanMutate(false, "public"));
        Assert.IsFalse(guard.CanMutate(false, null));
    }
}

