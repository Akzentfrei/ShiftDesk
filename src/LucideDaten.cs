// Icons von Lucide (https://lucide.dev), lucide-static v0.452.0, ISC-Lizenz.
// Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part of Feather (MIT). All other copyright (c) for Lucide are held by Lucide Contributors 2022.
using System.Collections.Generic;

namespace ShiftDesk
{
    static partial class Lucide
    {
        static readonly Dictionary<string, string> Daten = new Dictionary<string, string>
        {
            { "calendar-days", "<path d='M8 2v4'/><path d='M16 2v4'/><rect width='18' height='18' x='3' y='4' rx='2'/><path d='M3 10h18'/><path d='M8 14h.01'/><path d='M12 14h.01'/><path d='M16 14h.01'/><path d='M8 18h.01'/><path d='M12 18h.01'/><path d='M16 18h.01'/>" },
            { "car", "<path d='M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2'/><circle cx='7' cy='17' r='2'/><path d='M9 17h6'/><circle cx='17' cy='17' r='2'/>" },
            { "chart-column","<path d='M3 3v16a2 2 0 0 0 2 2h16'/><path d='M18 17V9'/><path d='M13 17V5'/><path d='M8 17v-3'/>" },
            { "chevron-down", "<path d='m6 9 6 6 6-6'/>" },
            { "chevron-left", "<path d='m15 18-6-6 6-6'/>" },
            { "chevron-right", "<path d='m9 18 6-6-6-6'/>" },
            { "chevron-up", "<path d='m18 15-6-6-6 6'/>" },
            { "circle-check", "<circle cx='12' cy='12' r='10'/><path d='m9 12 2 2 4-4'/>" },
            { "file-down", "<path d='M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z'/><path d='M14 2v4a2 2 0 0 0 2 2h4'/><path d='M12 18v-6'/><path d='m9 15 3 3 3-3'/>" },
            { "file-plus", "<path d='M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z'/><path d='M14 2v4a2 2 0 0 0 2 2h4'/><path d='M9 15h6'/><path d='M12 18v-6'/>" },
            { "folder-open", "<path d='m6 14 1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2'/>" },
            { "map-pin", "<path d='M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0'/><circle cx='12' cy='10' r='3'/>" },
            { "moon", "<path d='M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z'/>" },
            { "plus", "<path d='M5 12h14'/><path d='M12 5v14'/>" },
            { "printer", "<path d='M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2'/><path d='M6 9V3a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v6'/><rect x='6' y='14' width='12' height='8' rx='1'/>" },
            { "rotate-ccw", "<path d='M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8'/><path d='M3 3v5h5'/>" },
            { "save", "<path d='M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z'/><path d='M17 21v-7a1 1 0 0 0-1-1H8a1 1 0 0 0-1 1v7'/><path d='M7 3v4a1 1 0 0 0 1 1h7'/>" },
            { "save-all", "<path d='M10 2v3a1 1 0 0 0 1 1h5'/><path d='M18 18v-6a1 1 0 0 0-1-1h-6a1 1 0 0 0-1 1v6'/><path d='M18 22H4a2 2 0 0 1-2-2V6'/><path d='M8 18a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9.172a2 2 0 0 1 1.414.586l2.828 2.828A2 2 0 0 1 22 6.828V16a2 2 0 0 1-2.01 2z'/>" },
            { "shield-check", "<path d='M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z'/><path d='m9 12 2 2 4-4'/>" },
            { "snowflake", "<line x1='2' x2='22' y1='12' y2='12'/><line x1='12' x2='12' y1='2' y2='22'/><path d='m20 16-4-4 4-4'/><path d='m4 8 4 4-4 4'/><path d='m16 4-4 4-4-4'/><path d='m8 20 4-4 4 4'/>" },
            { "sparkles","<path d='M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z'/><path d='M20 3v4'/><path d='M22 5h-4'/><path d='M4 17v2'/><path d='M5 18H3'/>" },
            { "trash-2", "<path d='M3 6h18'/><path d='M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6'/><path d='M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2'/><line x1='10' x2='10' y1='11' y2='17'/><line x1='14' x2='14' y1='11' y2='17'/>" },
            { "triangle-alert", "<path d='m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3'/><path d='M12 9v4'/><path d='M12 17h.01'/>" },
            { "users", "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M22 21v-2a4 4 0 0 0-3-3.87'/><path d='M16 3.13a4 4 0 0 1 0 7.75'/>" },
            { "utensils", "<path d='M3 2v7c0 1.1.9 2 2 2h4a2 2 0 0 0 2-2V2'/><path d='M7 2v20'/><path d='M21 15V2a5 5 0 0 0-5 5v6c0 1.1.9 2 2 2h3Zm0 0v7'/>" },
            { "zoom-in", "<circle cx='11' cy='11' r='8'/><line x1='21' x2='16.65' y1='21' y2='16.65'/><line x1='11' x2='11' y1='8' y2='14'/><line x1='8' x2='14' y1='11' y2='11'/>" },
            { "zoom-out", "<circle cx='11' cy='11' r='8'/><line x1='21' x2='16.65' y1='21' y2='16.65'/><line x1='8' x2='14' y1='11' y2='11'/>" },
            { "maximize", "<path d='M8 3H5a2 2 0 0 0-2 2v3'/><path d='M21 8V5a2 2 0 0 0-2-2h-3'/><path d='M3 16v3a2 2 0 0 0 2 2h3'/><path d='M16 21h3a2 2 0 0 0 2-2v-3'/>" },
            { "download", "<path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/><polyline points='7 10 12 15 17 10'/><line x1='12' x2='12' y1='15' y2='3'/>" },
            { "check", "<path d='M20 6 9 17l-5-5'/>" },
            { "id-card", "<path d='M16 10h2'/><path d='M16 14h2'/><path d='M6.17 15a3 3 0 0 1 5.66 0'/><circle cx='9' cy='11' r='2'/><rect x='2' y='5' width='20' height='14' rx='2'/>" },
            { "file-spreadsheet", "<path d='M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z'/><path d='M14 2v4a2 2 0 0 0 2 2h4'/><path d='M8 13h2'/><path d='M14 13h2'/><path d='M8 17h2'/><path d='M14 17h2'/>" },
            { "info", "<circle cx='12' cy='12' r='10'/><path d='M12 16v-4'/><path d='M12 8h.01'/>" },
            { "circle-x", "<circle cx='12' cy='12' r='10'/><path d='m15 9-6 6'/><path d='m9 9 6 6'/>" },
            { "file-check", "<path d='M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z'/><path d='M14 2v4a2 2 0 0 0 2 2h4'/><path d='m9 15 2 2 4-4'/>" },
        };
    }
}
