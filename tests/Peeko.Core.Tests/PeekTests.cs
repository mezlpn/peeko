using Peeko.Core.Peeks;

namespace Peeko.Core.Tests;

public class BatteryPeekLogicTests
{
    private readonly BatteryPeekLogic _logic = new();

    [Fact]
    public void First_reading_never_peeks()
    {
        Assert.Null(_logic.Update(onAc: false, percent: 15, remaining: null));
    }

    [Fact]
    public void Plugging_and_unplugging_charger_peek()
    {
        _logic.Update(false, 60, null);
        Assert.Equal(PeekKind.Charging, _logic.Update(true, 60, null)?.Kind);

        var unplug = _logic.Update(false, 61, TimeSpan.FromMinutes(200));
        Assert.Equal(PeekKind.Discharging, unplug?.Kind);
        Assert.Equal("±3 j 20 m", unplug?.Detail);
    }

    [Fact]
    public void Low_and_critical_warn_once_each()
    {
        _logic.Update(false, 25, null);
        Assert.Null(_logic.Update(false, 21, null));
        Assert.Equal(PeekKind.BatteryLow, _logic.Update(false, 20, null)?.Kind);
        Assert.Null(_logic.Update(false, 19, null));
        Assert.Equal(PeekKind.BatteryCritical, _logic.Update(false, 10, null)?.Kind);
        Assert.Null(_logic.Update(false, 9, null));
    }

    [Fact]
    public void Charging_resets_warnings()
    {
        _logic.Update(false, 25, null);
        _logic.Update(false, 18, null);
        _logic.Update(true, 18, null);
        _logic.Update(false, 25, null);
        Assert.Equal(PeekKind.BatteryLow, _logic.Update(false, 20, null)?.Kind);
    }

    [Fact]
    public void Critical_peek_bypasses_fullscreen_and_holds_longer()
    {
        var critical = new PeekRequest(PeekKind.BatteryCritical);
        Assert.True(critical.BypassHidden);
        Assert.Equal(TimeSpan.FromSeconds(5), critical.Hold);
    }
}

public class WifiPeekLogicTests
{
    [Fact]
    public void Only_peeks_when_ssid_changes()
    {
        var logic = new WifiPeekLogic();
        Assert.Null(logic.Update("Rumah_5G"));
        Assert.Null(logic.Update("Rumah_5G"));

        var lost = logic.Update(null);
        Assert.Equal((PeekKind.WifiDisconnected, "Rumah_5G"), (lost!.Kind, lost.Text));

        var back = logic.Update("Kantor");
        Assert.Equal((PeekKind.WifiConnected, "Kantor"), (back!.Kind, back.Text));
    }
}

public class KeyboardLanguageTests
{
    [Theory]
    [InlineData(0x0409, "EN", "English")]
    [InlineData(0x0421, "ID", "Bahasa Indonesia")]
    [InlineData(0x0411, "JA", "日本語")]
    public void Maps_lcid_to_code_and_name(int lcid, string code, string name)
    {
        Assert.Equal((code, name), KeyboardLanguage.FromLcid(lcid));
    }

    [Fact]
    public void Caps_lock_off_peek_is_shorter()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), new PeekRequest(PeekKind.CapsLock) { Flag = false }.Hold);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), new PeekRequest(PeekKind.CapsLock) { Flag = true }.Hold);
    }
}
