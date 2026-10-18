using Celah.Core.Notch;
using Celah.Core.Productivity;
using Celah.Core.Storage;
using Microsoft.Extensions.Time.Testing;

namespace Celah.Core.Tests;

public class FocusTimerTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
    private readonly FocusTimer _timer;

    public FocusTimerTests()
    {
        _timer = new FocusTimer(_time, new FocusSettings());
    }

    private void Minutes(double m)
    {
        _time.Advance(TimeSpan.FromMinutes(m));
        _timer.Tick();
    }

    [Fact]
    public void Ready_shows_full_duration()
    {
        Assert.Equal(FocusStatus.Ready, _timer.Status);
        Assert.Equal(TimeSpan.FromMinutes(25), _timer.Remaining);
    }

    [Fact]
    public void Running_counts_down_from_ends_at()
    {
        _timer.Start();
        Minutes(6.3);
        Assert.Equal(TimeSpan.FromMinutes(18.7), _timer.Remaining);
    }

    [Fact]
    public void Pause_freezes_and_resume_continues()
    {
        _timer.Start();
        Minutes(5);
        _timer.Pause();
        Minutes(30);
        Assert.Equal(TimeSpan.FromMinutes(20), _timer.Remaining);
        _timer.Start();
        Minutes(5);
        Assert.Equal(TimeSpan.FromMinutes(15), _timer.Remaining);
    }

    [Fact]
    public void Focus_end_starts_break_then_next_focus_waits()
    {
        var events = new List<(FocusMode, FocusMode)>();
        _timer.Completed += (a, b) => events.Add((a, b));

        _timer.Start();
        Minutes(25);
        Assert.Equal((FocusMode.ShortBreak, FocusStatus.Running), (_timer.Mode, _timer.Status));

        Minutes(5);
        Assert.Equal((FocusMode.Focus, FocusStatus.Ready, 2), (_timer.Mode, _timer.Status, _timer.Session));
        Assert.Equal([(FocusMode.Focus, FocusMode.ShortBreak), (FocusMode.ShortBreak, FocusMode.Focus)], events);
    }

    [Fact]
    public void Long_break_after_fourth_session_then_cycle_restarts()
    {
        for (int i = 0; i < 3; i++)
        {
            _timer.Start();
            Minutes(25);
            Minutes(5);
        }
        Assert.Equal(4, _timer.Session);
        _timer.Start();
        Minutes(25);
        Assert.Equal(FocusMode.LongBreak, _timer.Mode);
        Assert.Equal(4, _timer.CompletedSessions);
        Minutes(15);
        Assert.Equal((FocusMode.Focus, 1), (_timer.Mode, _timer.Session));
    }

    [Fact]
    public void Survives_sleep_by_using_ends_at()
    {
        _timer.Start();
        // Laptop tidur 40 menit: fokus selesai, istirahat juga lewat saat Tick pertama setelah bangun.
        _time.Advance(TimeSpan.FromMinutes(40));
        _timer.Tick();
        Assert.Equal(FocusMode.ShortBreak, _timer.Mode);
        Assert.Equal(TimeSpan.FromMinutes(5), _timer.Remaining);
    }

    [Fact]
    public void Snapshot_restores_running_timer()
    {
        _timer.Start();
        Minutes(10);
        var restored = new FocusTimer(_time, new FocusSettings(), _timer.Snapshot);
        Assert.Equal((FocusStatus.Running, TimeSpan.FromMinutes(15)), (restored.Status, restored.Remaining));
    }

    [Fact]
    public void Skip_moves_to_break_without_completed_event()
    {
        bool completed = false;
        _timer.Completed += (_, _) => completed = true;
        _timer.Start();
        _timer.Skip();
        Assert.Equal(FocusMode.ShortBreak, _timer.Mode);
        Assert.False(completed);
    }
}

public class WaterTrackerTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 1, 0, 0, TimeSpan.Zero));

    public WaterTrackerTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("WIB", TimeSpan.FromHours(7), "WIB", "WIB"));
    }

    [Fact]
    public void Reminder_due_one_interval_after_last_drink()
    {
        var water = new WaterTracker(_time);
        water.Drink();
        _time.Advance(TimeSpan.FromMinutes(59));
        Assert.False(water.IsDue);
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.True(water.IsDue);
    }

    [Fact]
    public void Shown_reminder_waits_another_interval()
    {
        var water = new WaterTracker(_time);
        water.Drink();
        _time.Advance(TimeSpan.FromMinutes(60));
        water.MarkReminded();
        Assert.False(water.IsDue);
        _time.Advance(TimeSpan.FromMinutes(60));
        Assert.True(water.IsDue);
    }

    [Fact]
    public void Goal_reached_stops_reminders_but_counts_extra()
    {
        var water = new WaterTracker(_time);
        for (int i = 0; i < 8; i++) water.Drink();
        Assert.True(water.GoalReached);
        Assert.Null(water.NextReminder);
        water.Drink();
        Assert.Equal(9, water.Glasses);
    }

    [Fact]
    public void Resets_at_local_midnight()
    {
        var water = new WaterTracker(_time); // 08:00 WIB
        water.Drink();
        water.Drink();
        _time.Advance(TimeSpan.FromHours(15)); // 23:00 WIB
        Assert.Equal(2, water.Glasses);
        _time.Advance(TimeSpan.FromHours(2)); // 01:00 WIB besok
        Assert.Equal(0, water.Glasses);
    }

    [Theory]
    [InlineData(1250, "1,25 L")]
    [InlineData(2000, "2 L")]
    [InlineData(500, "0,5 L")]
    public void Formats_liters(int ml, string expected) => Assert.Equal(expected, WaterTracker.Liters(ml));
}

public class TodoListTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 3, 0, 0, TimeSpan.Zero));

    [Fact]
    public void New_items_go_on_top_and_done_items_sink()
    {
        var list = new TodoList(_time);
        var a = list.Add("Bayar listrik")!;
        var b = list.Add("Kirim laporan")!;
        Assert.Equal(["Kirim laporan", "Bayar listrik"], list.Items.Select(i => i.Text));

        list.Toggle(b.Id);
        Assert.Equal(["Bayar listrik", "Kirim laporan"], list.Items.Select(i => i.Text));
        Assert.Equal(1, list.DoneCount);
    }

    [Fact]
    public void Blank_text_is_ignored_and_long_text_trimmed()
    {
        var list = new TodoList(_time);
        Assert.Null(list.Add("   "));
        Assert.Equal(TodoList.MaxLength, list.Add(new string('x', 500))!.Text.Length);
    }

    [Fact]
    public void Done_items_from_previous_days_are_cleaned_up()
    {
        var list = new TodoList(_time);
        var a = list.Add("Lama")!;
        list.Add("Masih jalan");
        list.Toggle(a.Id);

        _time.Advance(TimeSpan.FromDays(1));
        var reloaded = new TodoList(_time, list.Data);
        Assert.Equal(["Masih jalan"], reloaded.Items.Select(i => i.Text));
    }

    [Fact]
    public void Reorders_open_items()
    {
        var list = new TodoList(_time);
        var c = list.Add("c")!;
        list.Add("b");
        list.Add("a");
        list.Move(c.Id, 0);
        Assert.Equal(["c", "a", "b"], list.Items.Select(i => i.Text));
    }
}

public sealed class ShelfTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "celah-shelf-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new();

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string MakeFile(string folder, string name)
    {
        var dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "isi");
        return path;
    }

    [Fact]
    public void Links_normal_files_and_copies_temporary_ones()
    {
        var docs = MakeFile("dokumen", "laporan.pdf");
        var temp = MakeFile("tmp", "unduhan.jpg");
        var shelf = new Shelf(_time, Path.Combine(_root, "rak"), tempDir: Path.Combine(_root, "tmp"));

        Assert.Equal(2, shelf.Add([docs, temp]));
        var items = shelf.Data.Items;
        Assert.Equal(docs, items[0].Path);
        Assert.False(items[0].IsCopy);
        Assert.True(items[1].IsCopy);
        Assert.StartsWith(Path.Combine(_root, "rak"), items[1].Path);
    }

    [Fact]
    public void Removing_a_link_never_deletes_the_original()
    {
        var docs = MakeFile("dokumen", "laporan.pdf");
        var shelf = new Shelf(_time, Path.Combine(_root, "rak"), tempDir: Path.Combine(_root, "tmp"));
        shelf.Add([docs]);
        shelf.Clear();
        Assert.True(File.Exists(docs));
    }

    [Fact]
    public void Removing_a_copy_deletes_only_the_copy()
    {
        var temp = MakeFile("tmp", "unduhan.jpg");
        var shelf = new Shelf(_time, Path.Combine(_root, "rak"), tempDir: Path.Combine(_root, "tmp"));
        shelf.Add([temp]);
        var copy = shelf.Data.Items[0].Path;
        shelf.Remove(shelf.Data.Items[0].Id);
        Assert.False(File.Exists(copy));
        Assert.True(File.Exists(temp));
    }

    [Fact]
    public void Duplicates_and_missing_files_are_skipped()
    {
        var docs = MakeFile("dokumen", "laporan.pdf");
        var shelf = new Shelf(_time, Path.Combine(_root, "rak"), tempDir: Path.Combine(_root, "tmp"));
        Assert.Equal(1, shelf.Add([docs, docs, Path.Combine(_root, "tidak-ada.txt")]));
    }

    [Fact]
    public void Files_moved_away_disappear_on_reload()
    {
        var docs = MakeFile("dokumen", "laporan.pdf");
        var shelf = new Shelf(_time, Path.Combine(_root, "rak"), tempDir: Path.Combine(_root, "tmp"));
        shelf.Add([docs]);
        File.Delete(docs);
        var reloaded = new Shelf(_time, Path.Combine(_root, "rak"), shelf.Data, Path.Combine(_root, "tmp"));
        Assert.Empty(reloaded.Items);
    }
}

public class ActivityArbiterTests
{
    [Theory]
    [InlineData(false, FocusStatus.Ready, false, CollapsedActivity.Idle)]
    [InlineData(true, FocusStatus.Ready, false, CollapsedActivity.Music)]
    [InlineData(false, FocusStatus.Running, false, CollapsedActivity.Focus)]
    [InlineData(false, FocusStatus.Paused, false, CollapsedActivity.Focus)]
    [InlineData(true, FocusStatus.Running, false, CollapsedActivity.MusicAndFocus)]
    [InlineData(true, FocusStatus.Running, true, CollapsedActivity.Water)]
    public void Picks_by_priority(bool music, FocusStatus focus, bool water, CollapsedActivity expected)
    {
        Assert.Equal(expected, ActivityArbiter.Pick(music, focus, water));
    }
}

public sealed class JsonFileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "celah-store-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Round_trips_and_survives_corrupt_file()
    {
        var store = new JsonFileStore<TodoData>(_dir, "todo.json");
        store.Save(new TodoData { Items = [new TodoItem { Text = "a" }] });
        Assert.Equal("a", store.Load().Items[0].Text);

        File.WriteAllText(Path.Combine(_dir, "todo.json"), "{rusak");
        Assert.Empty(store.Load().Items);
        Assert.True(File.Exists(Path.Combine(_dir, "todo.json.rusak")));
    }
}
