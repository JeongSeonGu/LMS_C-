using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using LmsAgent.Forms;
using Timer = System.Windows.Forms.Timer;

namespace LmsAgent.Services;

/// <summary>
/// 업무 일지와는 별개의 독립 창인 스티커 메모(<see cref="StickyNoteForm"/>) 여러 개를 관리합니다.
/// 서버와 무관한 순수 로컬 기능으로, 내용·위치·크기를 %AppData%\LmsAgent\notes\ 아래에
/// RTF 파일(메모별) + manifest.json(위치·크기 목록)으로 저장합니다.
/// 생명주기는 업무 일지(WorkJournalService)의 활성화 여부를 그대로 따릅니다 — 업무 일지를
/// 켜면 저장해 둔 메모가 함께 나타나고, 끄면 메모도 함께 숨겨집니다(내용은 보존됩니다).
/// </summary>
public sealed class StickyNoteService : IDisposable
{
    private sealed class NoteRecord
    {
        public Guid Id { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; } = 260;
        public int Height { get; set; } = 300;
    }

    private static readonly string NotesDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LmsAgent", "notes");

    private static readonly string ManifestPath = Path.Combine(NotesDir, "manifest.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<Guid, StickyNoteForm> _notes = new();
    private readonly Timer _saveTimer = new() { Interval = 1000 };
    private bool _loaded;

    public StickyNoteService()
    {
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveAll();
        };
    }

    /// <summary>🗒 버튼을 눌렀을 때 호출합니다. 저장된 메모를 복원해서 보여주고, 저장된
    /// 메모가 하나도 없으면(최초 사용) 빈 메모를 하나 새로 만듭니다 — 사용자가 직접
    /// "메모를 쓰겠다"고 누른 것이므로 빈 메모를 새로 만들어도 됩니다.</summary>
    public void ShowAll()
    {
        EnsureLoaded();

        if (_notes.Count == 0)
        {
            CreateNew();
            return;
        }

        foreach (var note in _notes.Values)
        {
            note.Show();
        }
    }

    /// <summary>업무 일지가 켜질 때(로그인 직후 포함) 자동으로 호출합니다. 예전에 써 둔
    /// 메모가 있으면 사용자가 🗒 버튼을 다시 누르지 않아도 그대로 복원해서 보여줍니다.
    /// <see cref="ShowAll"/>과 달리, 저장된 메모가 하나도 없을 때는 빈 메모를 새로 만들지
    /// 않습니다 — 이건 사용자가 직접 요청한 동작이 아니라 자동 복원이므로, 써 본 적 없는
    /// 사용자에게 갑자기 빈 메모 창이 나타나면 안 됩니다.</summary>
    public void RestoreIfAny()
    {
        EnsureLoaded();

        foreach (var note in _notes.Values)
        {
            note.Show();
        }
    }

    /// <summary>업무 일지를 끌 때 호출합니다. 창만 숨기고 내용은 그대로 보존합니다.</summary>
    public void HideAll()
    {
        foreach (var note in _notes.Values)
        {
            note.Hide();
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        List<NoteRecord>? records = null;
        try
        {
            if (File.Exists(ManifestPath))
            {
                records = JsonSerializer.Deserialize<List<NoteRecord>>(File.ReadAllText(ManifestPath), JsonOptions);
            }
        }
        catch
        {
            // manifest가 손상되었으면 메모 없이 새로 시작한다.
        }

        if (records is null)
        {
            return;
        }

        foreach (var record in records)
        {
            var note = CreateForm(record.Id);
            note.Location = new Point(record.Left, record.Top);
            note.Size = new Size(record.Width, record.Height);

            try
            {
                var rtfPath = RtfPath(record.Id);
                if (File.Exists(rtfPath))
                {
                    note.LoadRtf(File.ReadAllText(rtfPath));
                }
            }
            catch
            {
                // 개별 메모 내용이 손상되었어도 나머지 메모는 계속 복원한다.
            }

            _notes[record.Id] = note;
        }
    }

    private void CreateNew()
    {
        var id = Guid.NewGuid();
        var note = CreateForm(id);

        // 여러 개를 연달아 만들 때 완전히 겹치지 않도록 조금씩 어긋나게 배치한다.
        var offset = 24 * (_notes.Count % 8);
        note.Location = new Point(80 + offset, 80 + offset);

        _notes[id] = note;
        note.Show();
        ScheduleSave();
    }

    private StickyNoteForm CreateForm(Guid id)
    {
        var note = new StickyNoteForm(id);
        note.NewNoteRequested += (_, _) => CreateNew();
        note.DeleteRequested += (_, _) => DeleteNote(id);
        note.ContentOrBoundsChanged += (_, _) => ScheduleSave();
        return note;
    }

    private void DeleteNote(Guid id)
    {
        if (_notes.Remove(id, out var note))
        {
            note.Hide();
            note.Dispose();
        }

        try
        {
            File.Delete(RtfPath(id));
        }
        catch
        {
            // 파일 삭제 실패는 무시한다(다음 저장 때 manifest에서는 이미 빠진다).
        }

        ScheduleSave();
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveAll()
    {
        try
        {
            Directory.CreateDirectory(NotesDir);

            var records = new List<NoteRecord>();
            foreach (var (id, note) in _notes)
            {
                records.Add(new NoteRecord
                {
                    Id = id,
                    Left = note.Location.X,
                    Top = note.Location.Y,
                    Width = note.Width,
                    Height = note.Height,
                });

                var rtf = note.SaveRtf();
                if (rtf is not null)
                {
                    File.WriteAllText(RtfPath(id), rtf);
                }
            }

            File.WriteAllText(ManifestPath, JsonSerializer.Serialize(records, JsonOptions));
        }
        catch
        {
            // 메모 저장 실패는 부가 기능이므로 조용히 무시한다.
        }
    }

    private static string RtfPath(Guid id) => Path.Combine(NotesDir, id.ToString("N") + ".rtf");

    public void Dispose()
    {
        _saveTimer.Stop();
        SaveAll();
        _saveTimer.Dispose();

        foreach (var note in _notes.Values)
        {
            note.Dispose();
        }
    }
}
