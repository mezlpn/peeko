using Peeko.Core.Productivity;

namespace Peeko.Core.Tests;

public class AiActionsTests
{
    [Fact]
    public void Separates_actions_from_the_visible_answer()
    {
        var (text, actions) = AiActions.Parse("Siap, sudah kucatat.\n[[tugas: besok jam 7 kumpul laporan]]\n\n\n[[volume: 140]]\n[[musik: pause]]");
        Assert.Equal("Siap, sudah kucatat.", text);
        Assert.Equal([new AiAction(AiActionKind.Task, "besok jam 7 kumpul laporan"), new AiAction(AiActionKind.Volume, "100"), new AiAction(AiActionKind.Music, "jeda")], actions);
    }

    [Fact]
    public void Ignores_unknown_or_malformed_actions()
    {
        var (text, actions) = AiActions.Parse("Oke [[jalankan: format c:]] [[musik: dansa]] [[volume: keras]]");
        Assert.Empty(actions.Where(a => a.Kind != AiActionKind.Open));
        Assert.Empty(actions);
        Assert.Contains("[[jalankan: format c:]]", text);
    }
}
