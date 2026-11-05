using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Peeko.App.Animation;
using Peeko.Core.Productivity;

namespace Peeko.App.Productivity;

public partial class NotesModule : UserControl
{
    private NoteBook? _book;
    private Guid? _selected;
    private bool _loading;
    private StudySession? _study;

    public NotesModule()
    {
        InitializeComponent();
    }

    /// <summary>Mengetik dimulai/selesai: pemilik mengaktifkan jendela dan menahan panel terbuka.</summary>
    public event Action<bool>? TextInputChanged;

    /// <summary>Sedang belajar flashcard: panel ditahan terbuka.</summary>
    public event Action<bool>? StudyingChanged;

    public bool IsEditing { get; private set; }

    public void Attach(NoteBook book)
    {
        _book = book;
        book.RemoveEmpty();
        _selected = book.Notes.FirstOrDefault()?.Id;
        book.Changed += RebuildList;
        RebuildList();
        LoadSelected();
    }

    private void RebuildList()
    {
        if (_book is null) return;
        List.Children.Clear();
        foreach (var note in _book.Notes)
        {
            bool on = note.Id == _selected;
            var item = new Border
            {
                Height = 30,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 0, 6, 0),
                Margin = new Thickness(0, 0, 0, 2),
                Background = on ? B("SurfaceHover") : Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = NoteBook.Title(note),
                    Style = (Style)FindResource("Peeko.Text.Body"),
                    FontSize = 12.5,
                    Foreground = on ? B("TextPrimary") : B("TextSecondary"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            var id = note.Id;
            item.MouseEnter += (_, _) => { if (id != _selected) item.Background = B("SurfaceRaised"); };
            item.MouseLeave += (_, _) => { if (id != _selected) item.Background = Brushes.Transparent; };
            item.MouseLeftButtonUp += (_, _) => Select(id);
            List.Children.Add(item);
        }
        if (_book.Notes.Count == 0)
        {
            List.Children.Add(new TextBlock
            {
                Text = "Belum ada catatan",
                Style = (Style)FindResource("Peeko.Text.Caption"),
                Margin = new Thickness(8, 6, 0, 0),
            });
        }
    }

    private void Select(Guid id)
    {
        StopStudy();
        _selected = id;
        _book?.RemoveEmpty();
        RebuildList();
        LoadSelected();
    }

    private void LoadSelected()
    {
        _loading = true;
        var note = _selected is { } id ? _book?.Find(id) : null;
        Editor.Text = note?.Text ?? "";
        _loading = false;
        UpdateFooter();
    }

    private void UpdateFooter()
    {
        Placeholder.Visibility = Editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        int cards = StudySession.Parse(Editor.Text).Count;
        StudyButton.Visibility = cards > 0 ? Visibility.Visible : Visibility.Collapsed;
        StudyButton.Content = $"Belajar {cards} kartu";
        DeleteButton.Visibility = _selected is null ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── Mengetik ───────────────────────────────────────────────────────────

    private void OnNew(object sender, RoutedEventArgs e)
    {
        if (_book?.Add() is not { } note) return;
        Select(note.Id);
        BeginInput();
    }

    private void OnEditorMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsEditing) BeginInput();
    }

    private void BeginInput()
    {
        if (_book is null) return;
        if (_selected is null && _book.Add() is { } note) Select(note.Id);
        IsEditing = true;
        TextInputChanged?.Invoke(true);
        // Fokus setelah jendela diaktifkan oleh pemiliknya.
        Dispatcher.BeginInvoke(() => { Editor.Focus(); Keyboard.Focus(Editor); }, DispatcherPriority.Input);
    }

    /// <summary>Selesai mengetik (klik di luar, Esc, pindah tampilan).</summary>
    public void EndInput()
    {
        if (!IsEditing) return;
        IsEditing = false;
        TextInputChanged?.Invoke(false);
    }

    private void OnEditorLostFocus(object sender, KeyboardFocusChangedEventArgs e) => EndInput();

    private void OnEditorKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        EndInput();
        e.Handled = true;
    }

    private void OnEditorChanged(object sender, TextChangedEventArgs e)
    {
        UpdateFooter();
        if (_loading || _book is null || _selected is not { } id) return;
        _book.Update(id, Editor.Text);
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_book is null || _selected is not { } id) return;
        _book.Remove(id);
        _selected = _book.Notes.FirstOrDefault()?.Id;
        RebuildList();
        LoadSelected();
    }

    // ── Flashcard ──────────────────────────────────────────────────────────

    private void OnStudy(object sender, RoutedEventArgs e)
    {
        var cards = StudySession.Parse(Editor.Text);
        if (cards.Count == 0) return;
        EndInput();
        _study = new StudySession(cards);
        EditorView.Visibility = Visibility.Collapsed;
        StudyView.Visibility = Visibility.Visible;
        StudyingChanged?.Invoke(true);
        ShowCard();
    }

    private void ShowCard()
    {
        if (_study is null) return;
        if (_study.Finished)
        {
            CardSide.Text = "SELESAI";
            CardText.Text = $"{_study.Known} hafal · {_study.Again}× diulang";
            CardHint.Text = "";
            StudyButtons.Visibility = Visibility.Collapsed;
            RestartButton.Visibility = Visibility.Visible;
            return;
        }
        var card = _study.Current!;
        CardSide.Text = _study.Flipped ? "JAWABAN" : "PERTANYAAN";
        CardSide.Foreground = _study.Flipped ? B("Accent") : B("TextTertiary");
        CardText.Text = _study.Flipped ? card.Answer : card.Question;
        CardHint.Text = $"{_study.Known + 1}/{_study.Total}" + (_study.Flipped ? "" : " · klik untuk membalik");
        StudyButtons.Visibility = Visibility.Visible;
        RestartButton.Visibility = Visibility.Collapsed;
    }

    private void OnFlip(object sender, MouseButtonEventArgs e)
    {
        if (_study is null || _study.Finished) return;
        _study.Flip();
        if (!Motion.Reduced)
        {
            // Kartu "membalik": menyempit lalu melebar lagi.
            var a = Motion.To(1, Motion.Base, Motion.Out);
            a.From = 0.6;
            CardScale.Animate(System.Windows.Media.ScaleTransform.ScaleXProperty, a);
        }
        ShowCard();
    }

    private void OnKnown(object sender, RoutedEventArgs e)
    {
        _study?.MarkKnown();
        ShowCard();
    }

    private void OnAgain(object sender, RoutedEventArgs e)
    {
        _study?.MarkAgain();
        ShowCard();
    }

    private void OnStopStudy(object sender, RoutedEventArgs e) => StopStudy();

    public void StopStudy()
    {
        if (_study is null) return;
        _study = null;
        StudyView.Visibility = Visibility.Collapsed;
        EditorView.Visibility = Visibility.Visible;
        StudyingChanged?.Invoke(false);
    }

    private static Brush B(string token) => (Brush)Application.Current.Resources[$"Peeko.Brush.{token}"];
}
