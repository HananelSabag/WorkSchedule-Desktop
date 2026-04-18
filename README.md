# 🗓️ WorkSchedule Desktop — Smart Shift Management for Windows

A modern, full-featured **Windows desktop application** for intelligent work shift scheduling, built with C# and WPF (.NET 10).

> **Desktop port of the original Android app** — same powerful engine, redesigned for Windows with a native Material Design UI.

---

## 👨‍💻 About the Developer

**Hananel Sabag** — Software Engineering Graduate

I built this application from scratch as a full-stack desktop project, demonstrating my ability to deliver production-ready software with a polished UI, sophisticated algorithms, and clean architecture.

**This project showcases:**
- ✅ Full WPF / .NET desktop application development
- ✅ Constraint-satisfaction algorithm (MRV) implementation
- ✅ Clean MVVM architecture with Entity Framework Core
- ✅ Complete Hebrew RTL UI with Material Design theming
- ✅ Real-world UX: draft system, conflict resolution, multi-format export
- ✅ Installer packaging with Inno Setup (self-contained, no dependencies)

📧 hananel12345@gmail.com | **Currently seeking software development opportunities!**

---

## ✨ Key Features

### 📋 Smart Shift Template System
- Define custom shifts (name + hours) and working days
- One active template applies to all schedules
- Live preview of the table structure while editing

### 👥 Employee Management
- Add / edit / delete employees
- **Shabbat observer** flag — automatically blocks Friday afternoon + Saturday shifts
- **Mitgaber** flag — allows extended daily hours (16h instead of 12h max)

### 🚫 Constraint-Based Blocking
- **Cannot mode** — employee unavailable for selected shifts
- **Can-Only mode** — employee available *only* for selected shifts
- Auto Shabbat blocking for observers
- Visual hints (red / blue) on the scheduling grid

### 🤖 Automatic Schedule Generation
- **MRV greedy algorithm** (Minimum Remaining Values) with repair pass
- Hard constraints: Cannot/Shabbat blocks, max hours/day, no overlapping shifts, minimum rest between shifts
- Soft constraints: fair distribution, rest period scoring, night-shift grouping, streak penalty
- Impossible-shift detection with user-friendly summary

### ✍️ Manual Scheduling
- Click to assign selected employee to any cell
- Right-click for free-text editing (e.g. "David until 19:00")
- Real-time constraint hints directly on the grid
- Blocked-employee chips shown per cell

### 👁️ Preview & Share
- Fully editable preview grid after saving
- **Export to PNG** — high-quality rendered image (150 DPI), RTL-correct layout
- **Export to Excel** — styled headers, borders, auto-sized columns
- **WhatsApp Share** — renders image to clipboard + auto-launches WhatsApp

### 💾 History & Draft System
- All saved schedules stored in local SQLite database
- Rename, delete, and reopen any past schedule
- **Auto-draft**: saves your in-progress work when closing mid-flow
- "Continue Draft" banner on restart

---

## 🛠️ Technologies

### Core Stack
| Technology | Role |
|-----------|------|
| **C# / .NET 10** | Language & runtime |
| **WPF** | Windows desktop UI framework |
| **XAML** | Declarative UI layout |
| **Material Design in XAML** | Design system & theming |

### Architecture & Data
| Technology | Role |
|-----------|------|
| **MVVM** | Architecture pattern |
| **Entity Framework Core 10** | ORM layer |
| **SQLite** | Local database (auto-created, zero-config) |
| **CommunityToolkit.Mvvm** | MVVM helpers |
| **Newtonsoft.Json** | JSON serialization for schedule data |

### Export & Packaging
| Technology | Role |
|-----------|------|
| **ClosedXML** | Excel (.xlsx) export |
| **WPF RenderTargetBitmap** | PNG image rendering |
| **Inno Setup 6** | Windows installer packaging |

---

## 🖥️ System Requirements

| | Requirement |
|--|--|
| **OS** | Windows 10 (1809+) / Windows 11 |
| **Architecture** | x64 |
| **RAM** | 512 MB |
| **Disk** | ~250 MB |
| **.NET** | **Not required** — fully self-contained |

---

## 🚀 Installation

### Option A — Installer (Recommended)
1. Download **`WorkSchedule-Setup-1.0.0.exe`** from the repository root
2. Run the installer (no admin required)
3. Follow the setup wizard
4. Launch from Desktop shortcut or Start Menu

### Option B — Portable
Run `publish\win-x64\WorkSchedule.exe` directly — no installation needed.  
Data is stored at `%LocalAppData%\WorkSchedule\workschedule.db`.

---

## 🏗️ Build from Source

### Requirements
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022+ / JetBrains Rider (optional)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (installer only)

### Steps
```bash
# Clone
git clone https://github.com/HananelSabag/WorkSchedule-Desktop.git
cd WorkSchedule-Desktop

# Build & publish (self-contained EXE + installer)
publish.bat
```

Output:
- `publish\win-x64\WorkSchedule.exe` — portable executable
- `installer\Output\WorkSchedule-Setup-1.0.0.exe` — full installer

---

## 📁 Project Structure

```
WorkSchedule-Desktop/
├── WorkSchedule/
│   ├── Models/
│   │   ├── Employee.cs               # Employee entity
│   │   ├── Schedule.cs               # Schedule entity
│   │   ├── ShiftTemplate.cs          # Template + ShiftRow + DayColumn
│   │   ├── ScheduleFlowState.cs      # In-memory flow state (singleton)
│   │   └── GenericScheduleGenerator.cs  # MRV constraint-satisfaction algorithm
│   ├── Data/
│   │   ├── AppDbContext.cs           # EF Core DbContext (SQLite)
│   │   └── DatabaseSeeder.cs         # Default template seeder
│   ├── Views/
│   │   ├── MainWindow.xaml/.cs       # Shell + sidebar navigation
│   │   ├── AppDialog.cs              # Custom Hebrew-styled dialog system
│   │   └── Pages/
│   │       ├── HomePage              # Dashboard
│   │       ├── EmployeesPage         # Employee CRUD
│   │       ├── TemplatePage          # Shift template editor
│   │       ├── BlockingPage          # Availability constraints
│   │       ├── ManualSchedulePage    # Manual assignment grid
│   │       ├── AutoSchedulePage      # Algorithm result + review
│   │       ├── PreviewPage           # Edit + export + share
│   │       ├── HistoryPage           # Saved schedules browser
│   │       └── SettingsPage          # App settings
│   └── Themes/
│       └── AppTheme.xaml             # Global Material Design theme
├── installer/
│   └── setup.iss                     # Inno Setup 6 script
├── publish.bat                       # One-click build + installer script
├── WorkSchedule-Setup-1.0.0.exe      # Ready-to-use installer
└── README.md
```

---

## 🔄 How It Works

### Scheduling Flow
```
Home → [New Schedule]
  ↓
BlockingPage        — define who can't / can-only work which shifts
  ↓
ManualSchedulePage  — assign employees manually  ─┐
        OR                                         ├→ PreviewPage → Export / Share
AutoSchedulePage    — algorithm fills the grid   ─┘
```

### Algorithm (GenericScheduleGenerator)
1. **Phase 1 — Greedy MRV**: sort slots by fewest available employees → assign highest-scoring eligible employee
2. **Phase 2 — Repair**: attempt swaps for any still-empty slots
3. **Scoring** accounts for: assignment count, rest hours, night-shift adjacency, multi-day streaks

---

## 📊 Version History

### v1.0.0 (Current)
- 🎉 Initial Windows desktop release
- ✅ Full manual + automatic scheduling
- ✅ Complete blocking system (Cannot / Can-Only / Shabbat auto-blocks)
- ✅ PNG + Excel export with RTL-correct layout
- ✅ WhatsApp share (clipboard + auto-launch)
- ✅ Draft auto-save / resume
- ✅ History: rename, delete, reopen
- ✅ Custom AppDialog (Hebrew-styled, centered, themed)
- ✅ Inno Setup installer — self-contained, no .NET required

---

## 🔮 Planned Features
- 🔲 Reminder / notification system (Windows Task Scheduler)
- 🔲 Multiple active templates
- 🔲 Data backup to OneDrive / local folder
- 🔲 Dark mode toggle
- 🔲 Employee CSV import

---

## 📄 License & Copyright

```
Copyright © 2025 Hananel Sabag. All Rights Reserved.

This software and its source code are the exclusive intellectual property
of Hananel Sabag. No part of this software may be reproduced, distributed,
modified, sublicensed, or used in any form without prior written permission
from the author.

Unauthorized copying, commercial use, or redistribution is strictly prohibited.
```

---

## 📞 Contact

**Hananel Sabag** — Software Engineering Graduate  
📧 hananel12345@gmail.com  
🐙 [github.com/HananelSabag](https://github.com/HananelSabag)

---

*Built with ❤️ in Israel 🇮🇱 — WPF & .NET 10*
