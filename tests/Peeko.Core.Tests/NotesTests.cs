using Microsoft.Extensions.Time.Testing;
using Peeko.Core.Productivity;

namespace Peeko.Core.Tests;

public class NoteBookTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 5, 4, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Latest_edit_is_on_top_and_title_is_first_line()
    {
        var book = new NoteBook(_time);
        var a = book.Add("Belanja\ntelur, gula")!;
        _time.Advance(TimeSpan.FromMinutes(1));
        var b = book.Add("\n  Ide video  \nnotch")!;
        Assert.Equal([b.Id, a.Id], book.Notes.Select(n => n.Id));
        Assert.Equal("Ide video", NoteBook.Title(b));

        _time.Advance(TimeSpan.FromMinutes(1));
        book.Update(a.Id, "Belanja\ntelur, gula, kopi");
        Assert.Equal(a.Id, book.Notes[0].Id);
        Assert.Equal("Catatan baru", NoteBook.Title(new Note()));
    }

    [Fact]
    public void Empty_notes_are_cleaned()
    {
        var book = new NoteBook(_time);
        book.Add();
        book.Add("isi");
        book.RemoveEmpty();
        Assert.Single(book.Notes);
    }
}

public class FlashcardTests
{
    [Fact]
    public void Parses_question_answer_lines_only()
    {
        var cards = StudySession.Parse("Biologi bab 3\n- mitokondria :: pembangkit energi sel\nribosom :: sintesis protein\nbukan kartu\n :: kosong\nATP::adenosin trifosfat :: energi");
        Assert.Equal(3, cards.Count);
        Assert.Equal(new Flashcard("mitokondria", "pembangkit energi sel"), cards[0]);
        Assert.Equal(new Flashcard("ATP", "adenosin trifosfat :: energi"), cards[2]);
    }

    [Fact]
    public void Again_requeues_and_known_finishes()
    {
        var session = new StudySession([new("a", "1"), new("b", "2")], new Random(1));
        var first = session.Current!;
        session.Flip();
        Assert.True(session.Flipped);
        session.MarkAgain();
        Assert.False(session.Flipped);
        Assert.Equal(1, session.Again);

        session.MarkKnown();
        session.MarkKnown();
        Assert.True(session.Finished);
        Assert.Equal(2, session.Known);
        Assert.Null(session.Current);
        Assert.NotNull(first);
    }
}
