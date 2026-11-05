using System.IO;
using Peeko.Core.Productivity;
using Peeko.Core.Storage;

namespace Peeko.App.Productivity;

/// <summary>Memuat dan menyimpan otomatis daftar tugas, rak file, dan catatan.</summary>
public sealed class ListsController : IDisposable
{
    private readonly AutoSaver _todoSaver;
    private readonly AutoSaver _shelfSaver;
    private readonly AutoSaver _notesSaver;

    public ListsController(TimeProvider time)
    {
        var todoStore = new JsonFileStore<TodoData>(App.DataDirectory, "todo.json");
        Todo = new TodoList(time, todoStore.Load());
        _todoSaver = new AutoSaver(() => todoStore.Save(Todo.Data));
        Todo.Changed += _todoSaver.MarkDirty;

        var shelfStore = new JsonFileStore<ShelfData>(App.DataDirectory, "shelf.json");
        Shelf = new Shelf(time, Path.Combine(App.DataDirectory, "shelf"), shelfStore.Load());
        _shelfSaver = new AutoSaver(() => shelfStore.Save(Shelf.Data));
        Shelf.Changed += _shelfSaver.MarkDirty;

        var notesStore = new JsonFileStore<NotesData>(App.DataDirectory, "notes.json");
        Notes = new NoteBook(time, notesStore.Load());
        _notesSaver = new AutoSaver(() => notesStore.Save(Notes.Data));
        Notes.Changed += _notesSaver.MarkDirty;
    }

    public NoteBook Notes { get; }

    public TodoList Todo { get; }

    public Shelf Shelf { get; }

    public void Dispose()
    {
        _todoSaver.Flush();
        _shelfSaver.Flush();
        _notesSaver.Flush();
    }
}
