using System.IO;
using Celah.Core.Productivity;
using Celah.Core.Storage;

namespace Celah.App.Productivity;

/// <summary>Memuat dan menyimpan otomatis daftar tugas dan rak file.</summary>
public sealed class ListsController : IDisposable
{
    private readonly AutoSaver _todoSaver;
    private readonly AutoSaver _shelfSaver;

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
    }

    public TodoList Todo { get; }

    public Shelf Shelf { get; }

    public void Dispose()
    {
        _todoSaver.Flush();
        _shelfSaver.Flush();
    }
}
