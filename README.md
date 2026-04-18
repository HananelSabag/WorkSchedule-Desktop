# סידור עבודה — Desktop

**Work Schedule Manager** — אפליקציית ניהול סידור עבודה שבועי לWindows

> © 2025 **Hananel Sabag** — כל הזכויות שמורות.  
> אין להעתיק, לשכפל, להפיץ או לשנות את הקוד ללא אישור מפורש בכתב מהמחבר.

---

## תיאור

אפליקציית Desktop חלונות (WPF / .NET 10) לניהול סידור עבודה שבועי עם:

- **שיבוץ ידני** — ממשק גריד אינטואיטיבי לשיבוץ עובדים
- **שיבוץ אוטומטי** — אלגוריתם MRV (Minimum Remaining Values) עם מעבר תיקון
- **חסימות** — הגדרת עובדים שלא יכולים / יכולים רק במשמרות מסוימות
- **תבניות** — הגדרת ימים ומשמרות מותאמים אישית
- **ייצוא** — תמונת PNG + קובץ Excel מעוצבים
- **שיתוף WhatsApp** — העתקה ללוח ופתיחת WhatsApp
- **היסטוריה** — שמירה, שינוי שם ומחיקה של סידורים שמורים
- **טיוטה אוטומטית** — שמירת טיוטה בסגירת האפליקציה

---

## דרישות מערכת

| פריט | דרישה |
|------|--------|
| מערכת הפעלה | Windows 10 (1809+) / Windows 11 |
| ארכיטקטורה | x64 |
| זיכרון | 512 MB RAM |
| דיסק | 250 MB פנוי |
| .NET | מוכלל (self-contained) — אין צורך בהתקנה נפרדת |

---

## התקנה

### אפשרות א — installer (מומלץ)

1. הורד את `WorkSchedule-Setup-1.0.0.exe`
2. הרץ כמנהל מערכת
3. עקוב אחר אשף ההתקנה

### אפשרות ב — portable

1. הורד את `WorkSchedule.exe`
2. הצב בתיקיה הרצויה
3. הרץ ישירות — הנתונים נשמרים ב-`%LocalAppData%\WorkSchedule\`

---

## בנייה מקוד מקור

### דרישות

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022 / Rider (אופציונלי)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (לבנייית installer בלבד)

### הוראות

```bat
# שיבוט הפרויקט
git clone https://github.com/hananel12345/WorkSchedule-Desktop.git

# בנייה ופרסום
cd WorkSchedule-Desktop
publish.bat
```

הסקריפט יבנה `publish\win-x64\WorkSchedule.exe` ואם Inno Setup מותקן — יצור גם `installer\Output\WorkSchedule-Setup-1.0.0.exe`.

---

## מבנה הפרויקט

```
WorkSchedule-Desktop/
├── WorkSchedule/              # פרויקט WPF ראשי
│   ├── Models/                # מודלי נתונים + EF Core DbContext
│   ├── Views/
│   │   ├── Pages/             # כל המסכים (HomePage, HistoryPage, ...)
│   │   ├── Components/        # רכיבי UI משותפים
│   │   └── AppDialog.cs       # דיאלוגים מותאמים עברית
│   ├── Services/              # GenericScheduleGenerator (אלגוריתם שיבוץ)
│   ├── ViewModels/            # ViewModels ו-ScheduleFlowState
│   └── Themes/                # עיצוב Material Design מותאם
├── installer/
│   └── setup.iss              # Inno Setup script
├── publish.bat                # סקריפט בנייה + פרסום
└── README.md
```

---

## טכנולוגיות

| טכנולוגיה | שימוש |
|-----------|--------|
| WPF / .NET 10 | מסגרת ה-UI |
| Material Design in XAML | עיצוב |
| Entity Framework Core 10 + SQLite | מסד נתונים |
| ClosedXML | ייצוא Excel |
| CommunityToolkit.Mvvm | תשתית MVVM |
| Newtonsoft.Json | סריאליזציה |
| Inno Setup 6 | installer |

---

## רישיון

```
Copyright © 2025 Hananel Sabag. All Rights Reserved.

This software and its source code are the exclusive property of Hananel Sabag.
No part of this software may be reproduced, distributed, modified, or used
in any form or by any means without prior written permission from the author.

Unauthorized copying, redistribution, or commercial use is strictly prohibited.
```

---

*פותח ע"י חננאל שבג — 2025*
