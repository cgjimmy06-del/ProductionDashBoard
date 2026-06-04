# -*- coding: utf-8 -*-
"""
FProductionDashBoard 使用者操作手冊生成器
輸出：Word (.docx) + PDF (via Word COM)
"""

import sys
from pathlib import Path
from datetime import date

OUTPUT_DIR = Path(__file__).parent
VERSION_TAG = "v2.6"
DOCX_PATH  = OUTPUT_DIR / f"UserManual_PDB_{VERSION_TAG}.docx"
PDF_PATH   = OUTPUT_DIR / f"UserManual_PDB_{VERSION_TAG}.pdf"
REVIEW_DATE = date.today().strftime("%Y-%m-%d")

# ─────────────────────────────────────────
# WORD GENERATION
# ─────────────────────────────────────────
def generate_manual():
    from docx import Document
    from docx.shared import Pt, Cm, RGBColor, Inches
    from docx.enum.text import WD_ALIGN_PARAGRAPH
    from docx.enum.table import WD_ALIGN_VERTICAL, WD_TABLE_ALIGNMENT
    from docx.oxml.ns import qn
    from docx.oxml import OxmlElement

    doc = Document()

    # ── 頁面設定 A4 ──
    for sec in doc.sections:
        sec.page_width   = Cm(21)
        sec.page_height  = Cm(29.7)
        sec.left_margin  = sec.right_margin  = Cm(2.5)
        sec.top_margin   = sec.bottom_margin = Cm(2)

    FONT_CN = "微軟正黑體"
    C_DARK  = RGBColor(0x2E, 0x40, 0x57)
    C_BLUE  = RGBColor(0x19, 0x76, 0xD2)
    C_GRAY  = RGBColor(0x75, 0x75, 0x75)
    C_WHITE = RGBColor(0xFF, 0xFF, 0xFF)
    C_WARN  = RGBColor(0xE6, 0x5C, 0x00)
    C_TIP   = RGBColor(0x2E, 0x7D, 0x32)

    thin_border_color = "CCCCCC"

    def set_cell_bg(cell, hex_color):
        tc = cell._tc
        tcPr = tc.get_or_add_tcPr()
        shd = OxmlElement("w:shd")
        shd.set(qn("w:val"), "clear")
        shd.set(qn("w:color"), "auto")
        shd.set(qn("w:fill"), hex_color)
        tcPr.append(shd)

    def set_table_border(tbl, color="CCCCCC", size="4"):
        tblPr = tbl._tbl.tblPr
        tblBorders = OxmlElement('w:tblBorders')
        for side in ('top','left','bottom','right','insideH','insideV'):
            el = OxmlElement(f'w:{side}')
            el.set(qn('w:val'), 'single')
            el.set(qn('w:sz'), size)
            el.set(qn('w:space'), '0')
            el.set(qn('w:color'), color)
            tblBorders.append(el)
        tblPr.append(tblBorders)

    def run(para, text, bold=False, size=11, color=None, italic=False):
        r = para.add_run(text)
        r.bold   = bold
        r.italic = italic
        r.font.size = Pt(size)
        r.font.name = FONT_CN
        r._element.rPr.rFonts.set(qn('w:eastAsia'), FONT_CN)
        if color: r.font.color.rgb = color
        return r

    def heading1(text):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p.paragraph_format.space_before = Pt(18)
        p.paragraph_format.space_after  = Pt(6)
        p.paragraph_format.keep_with_next = True
        run(p, text, bold=True, size=16, color=C_DARK)
        # underline via border
        pPr = p._element.get_or_add_pPr()
        pBdr = OxmlElement("w:pBdr")
        bot = OxmlElement("w:bottom")
        bot.set(qn("w:val"), "single"); bot.set(qn("w:sz"), "8")
        bot.set(qn("w:space"), "2");    bot.set(qn("w:color"), "2E4057")
        pBdr.append(bot); pPr.append(pBdr)
        return p

    def heading2(text):
        p = doc.add_paragraph()
        p.paragraph_format.space_before = Pt(12)
        p.paragraph_format.space_after  = Pt(4)
        p.paragraph_format.keep_with_next = True
        run(p, text, bold=True, size=13, color=C_BLUE)
        return p

    def heading3(text):
        p = doc.add_paragraph()
        p.paragraph_format.space_before = Pt(8)
        p.paragraph_format.space_after  = Pt(3)
        p.paragraph_format.keep_with_next = True
        run(p, text, bold=True, size=11, color=C_DARK)
        return p

    def body(text, size=10.5, indent_cm=0, color=None):
        p = doc.add_paragraph()
        p.paragraph_format.space_after = Pt(4)
        if indent_cm:
            p.paragraph_format.left_indent = Cm(indent_cm)
        run(p, text, size=size, color=color)
        return p

    def bullet(text, level=0, bold_prefix=None):
        p = doc.add_paragraph()
        p.paragraph_format.space_after  = Pt(2)
        p.paragraph_format.left_indent  = Cm(0.5 + level * 0.5)
        p.paragraph_format.first_line_indent = Cm(-0.4)
        marker = "•" if level == 0 else "◦"
        run(p, f"{marker} ", bold=False, size=10.5)
        if bold_prefix:
            run(p, bold_prefix, bold=True, size=10.5)
        run(p, text, size=10.5)
        return p

    def step(num, text, detail=None):
        p = doc.add_paragraph()
        p.paragraph_format.space_after  = Pt(3)
        p.paragraph_format.left_indent  = Cm(0.5)
        p.paragraph_format.first_line_indent = Cm(-0.5)
        run(p, f"步驟 {num}　", bold=True, size=10.5, color=C_BLUE)
        run(p, text, size=10.5)
        if detail:
            doc.add_paragraph()
            q = doc.paragraphs[-1]
            q.paragraph_format.left_indent = Cm(1.0)
            q.paragraph_format.space_after = Pt(2)
            run(q, detail, size=9.5, italic=True, color=C_GRAY)
        return p

    def note(text, kind="tip"):
        color_map = {"tip": ("E8F5E9","2E7D32","💡 提示"), "warn": ("FFF3E0","E65C00","⚠ 注意"), "info": ("E3F2FD","1565C0","ℹ 說明")}
        bg, fg_hex, label = color_map.get(kind, color_map["tip"])
        fg = RGBColor(int(fg_hex[:2],16), int(fg_hex[2:4],16), int(fg_hex[4:],16))
        tbl = doc.add_table(rows=1, cols=1)
        tbl.alignment = WD_TABLE_ALIGNMENT.LEFT
        set_cell_bg(tbl.rows[0].cells[0], bg)
        set_table_border(tbl, color=fg_hex, size="6")
        cell = tbl.rows[0].cells[0]
        p = cell.paragraphs[0]
        p.paragraph_format.left_indent = Cm(0.3)
        run(p, f"{label}　", bold=True, size=10, color=fg)
        run(p, text, size=10)
        doc.add_paragraph().paragraph_format.space_after = Pt(2)

    def img_placeholder(caption, width_cm=14, height_cm=7):
        """留下圖片佔位框，供後續補上截圖"""
        tbl = doc.add_table(rows=1, cols=1)
        tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
        set_cell_bg(tbl.rows[0].cells[0], "F0F4F8")
        set_table_border(tbl, color="AAAAAA", size="6")
        cell = tbl.rows[0].cells[0]
        # 設定高度
        tc = cell._tc
        trPr = tc.getparent().get_or_add_trPr()
        trHeight = OxmlElement("w:trHeight")
        trHeight.set(qn("w:val"), str(int(height_cm * 567)))
        trHeight.set(qn("w:hRule"), "exact")
        trPr.append(trHeight)
        # 設定寬度
        tbl.columns[0].width = Cm(width_cm)
        p = cell.paragraphs[0]
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        run(p, f"\n\n📷 圖片位置：{caption}\n\n", size=11, color=C_GRAY, italic=True)
        cap = doc.add_paragraph()
        cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
        cap.paragraph_format.space_after = Pt(8)
        run(cap, f"▲ {caption}", size=9.5, italic=True, color=C_GRAY)

    def simple_table(headers, rows_data, col_widths_cm=None):
        tbl = doc.add_table(rows=len(rows_data)+1, cols=len(headers))
        tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
        set_table_border(tbl)
        for ci, h in enumerate(headers):
            cell = tbl.rows[0].cells[ci]
            set_cell_bg(cell, "2E4057")
            cell.paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.CENTER
            run(cell.paragraphs[0], h, bold=True, size=10, color=C_WHITE)
        for ri, row in enumerate(rows_data, 1):
            for ci, val in enumerate(row):
                cell = tbl.rows[ri].cells[ci]
                if ri % 2 == 0:
                    set_cell_bg(cell, "F8F9FA")
                run(cell.paragraphs[0], str(val), size=10)
        if col_widths_cm:
            from docx.oxml.ns import qn as _qn
            for ci, w in enumerate(col_widths_cm):
                for ri in range(len(rows_data)+1):
                    tbl.rows[ri].cells[ci].width = Cm(w)
        doc.add_paragraph().paragraph_format.space_after = Pt(4)

    # ══════════════════════════════════════
    # 封面
    # ══════════════════════════════════════
    for _ in range(4): doc.add_paragraph()

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run(p, "FProductionDashBoard", bold=True, size=30, color=C_DARK)

    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run(p, "現場生產管理系統", bold=True, size=22, color=C_DARK)

    doc.add_paragraph()
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run(p, "使用者操作手冊  User Operation Manual", size=14, color=C_GRAY)

    doc.add_paragraph()
    # 分隔線
    hr_p = doc.add_paragraph()
    pPr = hr_p._element.get_or_add_pPr()
    pBdr = OxmlElement("w:pBdr")
    bot = OxmlElement("w:bottom")
    bot.set(qn("w:val"),"single"); bot.set(qn("w:sz"),"12")
    bot.set(qn("w:color"),"2E4057")
    pBdr.append(bot); pPr.append(pBdr)

    doc.add_paragraph()
    for label, value in [
        ("文件版本", f"{VERSION_TAG}（含硬體設定 / ABB機械手 / Modbus TCP 設備支援）"),
        ("適用對象", "現場操作人員、設備管理員、系統管理員"),
        ("語言版本", "繁體中文"),
        ("文件日期", REVIEW_DATE),
        ("文件狀態", "更新版"),
    ]:
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        run(p, f"{label}：", bold=True, size=11, color=C_DARK)
        run(p, value, size=11)

    doc.add_page_break()

    # ══════════════════════════════════════
    # 修訂紀錄
    # ══════════════════════════════════════
    heading1("文件修訂紀錄")
    simple_table(
        ["版本","日期","修訂人","修訂說明"],
        [
            ["v1.0", "2026-05-06", "—",                  "初版建立"],
            ["v1.1", "2026-05-13", "AI-assisted",        "補主畫面版面配置（PR#29）+ Menu 外觀切換（PR#32）"],
            ["v1.2", "2026-05-15", "AI-assisted",        "補日誌管理上傳功能（PR#36）+ 閒置偵測機制更新（PR#37）"],
            ["v1.3", "2026-05-21", "AI-assisted",        "補系統參數設定（PR#26）+ 章節對齊現況；輸出路徑改為 DB_outputFile"],
            ["v2.5", REVIEW_DATE,  "AI-assisted",        "新增 6.8 硬體設定（PR#62）；補 ABB 機械手說明；離線優化說明"],
            [VERSION_TAG, REVIEW_DATE, "AI-assisted",    "新增 7.3 Modbus TCP 設備（PR#64-65）；補設備卡片 Modbus 狀態說明；補 FAQ"],
        ],
        [2.0, 2.5, 3.5, 8.0]
    )
    doc.add_page_break()

    # ══════════════════════════════════════
    # 目錄（手動，頁碼待 Word 更新）
    # ══════════════════════════════════════
    heading1("目錄")
    toc_items = [
        ("1.", "系統簡介", ""),
        ("2.", "系統需求與安裝", ""),
        ("3.", "啟動與登入", ""),
        ("  3.1", "密碼登入", ""),
        ("  3.2", "刷卡登入", ""),
        ("  3.3", "訪客登入", ""),
        ("  3.4", "語言切換", ""),
        ("4.", "主介面說明", ""),
        ("  4.1", "導覽列", ""),
        ("  4.2", "狀態列", ""),
        ("  4.3", "主畫面版面配置", ""),
        ("  4.4", "功能表（Menu Bar）", ""),
        ("5.", "現場操作面板", ""),
        ("  5.1", "設備卡片說明", ""),
        ("  5.2", "新增／管理設備卡片", ""),
        ("  5.3", "物料更換", ""),
        ("  5.4", "首件檢查", ""),
        ("  5.5", "例行巡檢", ""),
        ("  5.6", "調試操作（帶點 / 調品質）", ""),
        ("6.", "系統設定", ""),
        ("  6.1", "設備管理", ""),
        ("  6.2", "員工管理", ""),
        ("  6.3", "材料管理", ""),
        ("  6.4", "錯誤清單管理", ""),
        ("  6.5", "巡檢時段設定", ""),
        ("  6.6", "角色與權限管理", ""),
        ("  6.7", "系統參數設定（業務時間 / 排程 / 閒置登出）", ""),
        ("  6.8", "硬體設定（讀卡機 / ABB 機械手 RAPID 位址）", ""),
        ("  6.9", "日誌管理與上傳", ""),
        ("7.",     "硬體設定（讀卡機 / Modbus TCP 設備）", ""),
        ("  7.1",  "新增讀卡機", ""),
        ("  7.2",  "移除讀卡機", ""),
        ("  7.3",  "Modbus TCP 設備", ""),
        ("  7.3.1","新增 Modbus TCP 連線", ""),
        ("  7.3.2","讀寫測試平台", ""),
        ("  7.3.3","移除 Modbus TCP 設備", ""),
        ("8.", "離線模式說明", ""),
        ("9.", "登出與閒置保護", ""),
        ("10.", "常見問題（FAQ）", ""),
    ]
    for num, title, page in toc_items:
        p = doc.add_paragraph()
        p.paragraph_format.space_after = Pt(1)
        indent = 0.8 if num.startswith("  ") else 0
        p.paragraph_format.left_indent = Cm(indent)
        run(p, f"{num.strip()}　{title}", size=10.5,
            bold=not num.startswith("  "), color=C_DARK if not num.startswith("  ") else None)

    doc.add_page_break()

    # ══════════════════════════════════════
    # 1. 系統簡介
    # ══════════════════════════════════════
    heading1("1. 系統簡介")
    body("FProductionDashBoard 是一套 WPF 桌面應用程式，專為現場生產管理設計。"
         "系統連結公司 SQL Server 資料庫，提供以下核心功能：")
    for feat in [
        "設備卡片看板：即時顯示各設備巡檢時段狀態",
        "現場操作記錄：首件檢查、例行巡檢、物料更換、調試操作一鍵記錄並上傳",
        "多語言支援：繁體中文 / English / Tiếng Việt 即時切換",
        "讀卡機整合：刷員工卡自動登入、換手，提升操作效率",
        "離線模式：網路中斷時自動緩存操作，恢復連線後自動同步",
        "角色權限控管：依角色顯示或隱藏不同功能模組",
    ]:
        bullet(feat)
    doc.add_page_break()

    # ══════════════════════════════════════
    # 2. 系統需求與安裝
    # ══════════════════════════════════════
    heading1("2. 系統需求與安裝")
    heading2("2.1 系統需求")
    simple_table(
        ["項目","需求"],
        [
            ["作業系統", "Windows 10 / Windows 11（64 位元）"],
            ["執行環境", ".NET 8 Desktop Runtime（安裝包已內含，ClickOnce 自動安裝）"],
            ["顯示解析度", "建議 1920×1080 以上"],
            ["網路", "可連線至公司 SQL Server（離線模式下可暫時使用）"],
            ["讀卡機", "串列埠（COM Port）讀卡機（選配）"],
        ],
        [3.5, 12.5]
    )

    heading2("2.2 安裝方式")
    step(1, "向系統管理員取得 ClickOnce 安裝連結")
    step(2, "在瀏覽器開啟連結，點擊「安裝」")
    step(3, "若出現 .NET 安裝提示，依指示完成安裝後重試", "安裝過程需要網路連線下載相關元件")
    step(4, "安裝完成後，桌面或開始選單將出現「FProductionDashBoard」捷徑")
    note("系統會自動檢查更新，有新版本時會提示使用者更新", kind="info")
    doc.add_page_break()

    # ══════════════════════════════════════
    # 3. 啟動與登入
    # ══════════════════════════════════════
    heading1("3. 啟動與登入")
    body("雙擊桌面捷徑或從開始選單啟動系統，系統載入完成後會顯示登入畫面。")
    img_placeholder("登入畫面總覽（含語言選擇、帳號欄位、登入 / 訪客按鈕）")

    heading2("3.1 密碼登入")
    step(1, "在「職號」欄位輸入員工職號")
    step(2, "在「密碼」欄位輸入密碼")
    step(3, "點擊「登入」按鈕")
    step(4, "系統驗證成功後自動進入主介面", "若帳號或密碼錯誤，將顯示錯誤提示，請重新輸入")
    note("初次使用請向管理員確認您的帳號密碼", kind="info")

    heading2("3.2 刷卡登入")
    step(1, "確認讀卡機已連線（狀態列右側讀卡機圖示顯示綠色）")
    step(2, "將員工識別卡靠近讀卡機感應區")
    step(3, "系統自動識別卡號並登入，無需手動輸入")
    step(4, "若卡號未在系統中登記，系統會提示查詢 ERP 資料並確認是否新增")
    img_placeholder("刷卡登入流程示意（讀卡機圖示 → 自動登入提示）")
    note("讀卡機連線狀態可在主介面右下角確認，紅色代表未連線", kind="warn")

    heading2("3.3 訪客登入")
    step(1, "直接點擊「訪客」按鈕")
    step(2, "以訪客身份登入後，部分功能依權限設定可能受限")
    note("訪客模式僅供查看，無法執行寫入操作（如物料更換、巡檢記錄）", kind="warn")

    heading2("3.4 語言切換")
    body("登入畫面右上角提供語言選擇，支援三種語言：")
    simple_table(
        ["選項","語言"],
        [["中文","繁體中文（預設）"], ["English","英文"], ["Tiếng Việt","越南文"]],
        [4, 12]
    )
    body("選擇語言後介面文字立即切換，設定會記憶至下次啟動。")
    img_placeholder("語言切換下拉選單位置（登入畫面右上角）")
    doc.add_page_break()

    # ══════════════════════════════════════
    # 4. 主介面說明
    # ══════════════════════════════════════
    heading1("4. 主介面說明")
    img_placeholder("主介面整體佈局（導覽列 + 內容區 + 狀態列）", height_cm=9)

    heading2("4.1 導覽列")
    body("畫面左側為導覽列，點擊各按鈕切換功能模組。點擊最上方的折疊箭頭可收合 / 展開導覽列。")
    simple_table(
        ["導覽按鈕","功能說明","所需權限"],
        [
            ["🏠 首頁",      "系統首頁（預設畫面）",             "所有使用者"],
            ["🔧 現場操作面板","設備卡片看板，執行生產操作記錄",  "View 權限"],
            ["📊 圖表",      "生產數據圖表（功能開發中）",        "View 權限"],
            ["📋 清單管理",  "系統設定 CRUD 管理（設備 / 員工等）","View 權限"],
            ["🖥 設備管理",  "讀卡機等硬體設備設定",              "View 權限"],
            ["📦 排單管理",  "生產排程管理（功能開發中）",        "View 權限"],
        ],
        [3.5, 7, 3.5]
    )
    note("導覽按鈕的顯示依使用者角色權限而定，無權限的功能將無法點擊", kind="info")

    heading2("4.2 狀態列")
    body("畫面底部狀態列顯示系統即時資訊：")
    simple_table(
        ["區域","說明"],
        [
            ["左側 — 系統訊息","顯示最新操作結果與系統狀態（點擊圖釘可釘選最新訊息）"],
            ["中間 — 進度條",  "資料載入或上傳時顯示進度"],
            ["右側 — 連線狀態","🟢 已連線 / 🔴 離線；讀卡機連線圖示"],
            ["右側 — 使用者",  "目前登入的使用者名稱與登出按鈕"],
        ],
        [4.5, 11.5]
    )
    img_placeholder("狀態列各區域說明（箭頭標示各元件）")

    heading2("4.3 主畫面版面配置")
    body("透過畫面上方工具列的版型按鈕，可切換主畫面為四種版面配置，"
         "每個分割面板可獨立切換顯示內容（首頁 / 現場操作 / 設定 等）。")
    simple_table(
        ["版型","說明"],
        [
            ["單一",     "整個主畫面顯示單一面板（預設）"],
            ["左右分割", "畫面左右兩個面板，可同時顯示兩個不同功能模組"],
            ["上下分割", "畫面上下兩個面板"],
            ["四格",     "畫面分為四個等大面板，可同時監控多個區域"],
        ],
        [3, 13]
    )
    note("各面板獨立切換內容後，切換版型不會清除已選內容", kind="tip")
    img_placeholder("版型切換按鈕（工具列四個版型圖示）")

    heading2("4.4 功能表（Menu Bar）")
    body("畫面最上方功能表提供快速設定入口：")
    simple_table(
        ["功能表","項目","說明"],
        [
            ["檔案", "離開",        "關閉系統"],
            ["工具", "系統設定",     "開啟系統參數設定視窗（Ctrl+,）"],
            ["工具", "語言",        "快速切換 中文 / English / Tiếng Việt"],
            ["工具", "佈景主題",     "快速切換 淺色 / 深色 主題"],
        ],
        [2.5, 3.5, 10]
    )
    img_placeholder("功能表展開示意（工具 → 語言 / 佈景主題子選單）")
    doc.add_page_break()

    # ══════════════════════════════════════
    # 5. 現場操作面板
    # ══════════════════════════════════════
    heading1("5. 現場操作面板")
    body("點擊左側導覽「現場操作面板」進入設備卡片看板，此為日常操作的核心畫面。")
    img_placeholder("現場操作面板總覽（多張設備卡片排列）", height_cm=9)

    heading2("5.1 設備卡片說明")
    body("每張設備卡片代表一台設備，顯示以下資訊：")
    simple_table(
        ["資訊區域","說明"],
        [
            ["設備名稱 / ID",    "卡片頂部顯示設備識別資訊"],
            ["目前產品",         "ModelCode — TypeCode（如 ABC123 — 001）"],
            ["時段狀態指示燈",   "每個圓點代表一個巡檢時段\n🔵 未到時段  🟢 已巡檢  🔴 逾時未巡檢  ⚪ 不適用"],
            ["設備連線狀態",     "硬體設備類型（TypeId=1 ABB / TypeId=2 Modbus TCP）的卡片顯示連線燈號\n🟢 已連線  🔴 未連線（自動重連中）"],
            ["首件狀態",         "✅ 首件已完成  ❌ 首件未完成"],
            ["操作按鈕列",       "物料 / 首件 / 巡檢 / 調試 四個操作按鈕"],
            ["調試進行中",       "進行調試時卡片顯示計時器與「結束調試」按鈕"],
        ],
        [4, 12]
    )
    img_placeholder("設備卡片各區域說明（含時段狀態燈、操作按鈕標示）")

    heading2("5.2 新增 / 管理設備卡片")
    body("操作面板右上角工具列提供設備卡片管理功能：")
    simple_table(
        ["按鈕","功能"],
        [
            ["➕ 新增設備",    "手動新增一張設備卡片（從設備清單選擇）"],
            ["⚡ 快速上載",    "從已儲存的預設清單一次載入所有設備卡片"],
            ["💾 快速儲存",    "將目前卡片清單儲存為預設清單（下次快速上載使用）"],
            ["🗑 刪除設備",    "移除目前所有設備卡片（不影響資料庫記錄）"],
            ["✅ 統一首件",    "對所有設備同時執行首件操作"],
            ["🔄 統一巡檢",    "對所有設備同時執行例行巡檢"],
            ["🔃 更新清單",    "從資料庫重新載入設備 / 材料 / 員工等清單"],
        ],
        [4, 12]
    )
    img_placeholder("操作面板工具列各按鈕說明（箭頭標示）")
    note("快速上載前請先確認已執行過「快速儲存」，否則會載入上次儲存的清單", kind="warn")

    heading2("5.3 物料更換")
    step(1, "點擊設備卡片上的「物料」按鈕")
    step(2, "在彈出對話框中選擇更換的材料品項")
    step(3, "選擇換手員工（若有換手）")
    step(4, "點擊「確認」完成記錄，系統自動上傳至資料庫")
    img_placeholder("物料更換對話框（材料選單 + 員工選擇）")
    note("離線時操作會先暫存至本機，連線後自動補傳", kind="info")

    heading2("5.4 首件檢查")
    step(1, "點擊設備卡片上的「首件」按鈕")
    step(2, "在彈出對話框中選擇檢查結果：「正常」或「異常回報」")
    step(3, "若有異常，從錯誤清單選擇對應的錯誤代碼")
    step(4, "點擊「確認」送出，卡片上首件狀態自動更新為 ✅")
    img_placeholder("首件檢查對話框（結果選擇 + 異常錯誤碼清單）")
    note("每個生產班次開始時需完成首件，首件狀態燈亮起才可繼續後續巡檢", kind="warn")

    heading2("5.5 例行巡檢")
    step(1, "點擊設備卡片上的「巡檢」按鈕")
    step(2, "在彈出對話框中選擇巡檢結果：「正常」或「異常回報」")
    step(3, "若有異常，從錯誤清單選擇對應的錯誤代碼")
    step(4, "點擊「確認」送出，對應時段狀態指示燈更新為 🟢")
    img_placeholder("例行巡檢對話框（時段顯示 + 結果選擇）")
    body("系統會依巡檢時段設定自動判斷目前應巡哪個時段，逾時未巡檢的時段會標示為 🔴。")
    note("若系統偵測到逾時未巡檢，將自動補填異常紀錄，管理人員可在後台查詢", kind="info")

    heading2("5.6 調試操作（帶點 / 調品質）")
    body("調試功能用於記錄設備帶點（Teaching）或調品質（Offset）的作業時間。")
    step(1, "點擊設備卡片上的「調試」按鈕")
    step(2, "在彈出對話框中選擇調試類型：「帶點」或「調品質」")
    step(3, "點擊選擇後系統開始計時，卡片進入調試模式並顯示計時器")
    step(4, "調試完成後，點擊卡片上的「結束調試」按鈕")
    step(5, "系統出現刷卡或確認提示，確認後記錄上傳")
    img_placeholder("調試進行中的設備卡片（計時器 + 結束調試按鈕）")
    img_placeholder("結束調試確認畫面（刷卡或點擊確認）")
    note("調試過程中可切換至其他設備進行操作，計時器持續在背景運行", kind="tip")
    doc.add_page_break()

    # ══════════════════════════════════════
    # 6. 系統設定
    # ══════════════════════════════════════
    heading1("6. 系統設定")
    body("點擊左側導覽「清單管理」進入系統設定，包含六個設定頁籤。需具備對應權限才可存取。")
    img_placeholder("系統設定畫面（六個頁籤概覽）")

    heading2("6.1 設備管理")
    body("管理生產設備清單，包含設備 ID、名稱、廠區、棟別、樓層等資訊。")
    simple_table(
        ["操作","說明"],
        [
            ["新增","點擊 ➕ 新增按鈕，填入設備資料後儲存"],
            ["編輯","點擊列表中的設備，修改欄位後點擊儲存"],
            ["刪除","勾選設備後點擊刪除按鈕（確認提示後執行）"],
        ],
        [3, 13]
    )
    img_placeholder("設備管理頁籤（清單 + 新增/編輯表單）")

    heading2("6.2 員工管理")
    body("管理員工帳號，包含職號、姓名、卡號、角色等資訊。")
    simple_table(
        ["欄位","說明"],
        [
            ["職號（User ID）","員工識別編號，用於密碼登入"],
            ["姓名",           "顯示名稱"],
            ["卡號（Card ID）","讀卡機識別碼，用於刷卡登入"],
            ["角色",           "決定該員工的系統操作權限"],
            ["密碼",           "登入密碼（輸入後以 * 遮蔽）"],
        ],
        [4, 12]
    )
    img_placeholder("員工管理頁籤（員工清單 + 新增員工表單）")
    note("新增員工後，需指定角色才能賦予相應操作權限", kind="warn")

    heading2("6.3 材料管理")
    body("管理物料清單，包含材料名稱、廠牌、庫存資訊。")
    img_placeholder("材料管理頁籤（材料清單）")

    heading2("6.4 錯誤清單管理")
    body("管理異常回報時可選取的錯誤代碼與描述，支援多語翻譯預覽。")
    simple_table(
        ["欄位","說明"],
        [
            ["錯誤代碼","唯一識別碼，格式建議：類型名稱 + 序號（共 8 碼）"],
            ["優先級",  "Critical / High / Medium / Low"],
            ["翻譯",    "各語言對應的錯誤描述文字"],
            ["翻譯數量","目前已填入的語言翻譯筆數"],
        ],
        [4, 12]
    )
    img_placeholder("錯誤清單管理頁籤（含翻譯預覽欄）")

    heading2("6.5 巡檢時段設定")
    body("設定每日的巡檢時段，系統依此判斷每個時段是否已完成巡檢。")
    simple_table(
        ["欄位","說明"],
        [
            ["開始時間",  "時段起始時刻（格式：HH:mm）"],
            ["結束時間",  "時段結束時刻（格式：HH:mm）"],
            ["跨日",      "勾選後允許跨越 00:00 的時段（如 23:00 ~ 01:00）"],
            ["標籤",      "時段名稱，顯示於設備卡片工具提示"],
        ],
        [4, 12]
    )
    note("修改巡檢時段設定後，需重新啟動系統或手動重新載入才會生效", kind="warn")
    img_placeholder("巡檢時段設定頁籤（時段清單 + 新增表單）")

    heading2("6.6 角色與權限管理")
    body("設定不同角色所擁有的系統操作權限。")
    simple_table(
        ["欄位","說明"],
        [
            ["角色 ID",  "角色識別碼"],
            ["角色名稱", "角色顯示名稱（如：操作員、領班、管理員）"],
            ["權限",     "勾選此角色可使用的功能模組與操作項目"],
        ],
        [4, 12]
    )
    img_placeholder("角色權限管理頁籤（角色清單 + 權限勾選）")
    note("修改角色權限後，相關使用者需重新登入才會套用新權限", kind="info")

    heading2("6.7 系統參數設定")
    body("透過畫面上方功能表「工具 → 系統設定」（或快捷鍵 Ctrl+,）開啟系統參數視窗，"
         "可調整業務時間、自動同步、補檢與閒置登出的相關參數。")
    simple_table(
        ["設定區塊","欄位","說明"],
        [
            ["業務時間",       "業務日起始時 / 分", "定義每日業務日切換時間（預設 08:00），影響跨日時段判斷與報表彙整"],
            ["自動同步",       "啟用 / 間隔（秒）",  "啟用後系統依間隔時間將離線暫存資料上傳至資料庫（預設 60 秒）"],
            ["補檢檢測",       "啟用 / 間隔（秒）",  "啟用後系統定期掃描未巡檢時段並提示補檢（預設 300 秒）"],
            ["閒置自動登出",   "啟用 / 閒置秒數",    "使用者無操作達設定時間後，自動顯示登出倒數視窗（預設 600 秒）"],
        ],
        [3.5, 4.5, 8]
    )
    note("修改設定後請按「套用」儲存；未儲存的變更會在頂部顯示警告橫條提示", kind="warn")
    img_placeholder("系統設定頁面（業務時間 + 同步 + 補檢 + 閒置四區塊）")

    heading2("6.8 硬體設定（系統設定中的進階參數）")
    body("「系統設定」視窗中提供硬體設定區塊，可設定讀卡機連線參數與 ABB 機械手 RAPID 變數位址。"
         "修改後請點擊「套用」儲存至 hardware_config.json（儲存位置：%LOCALAPPDATA%\\FProductionDashBoard\\）。")
    heading3("讀卡機")
    simple_table(
        ["設定項","說明","預設值"],
        [
            ["COM Port",   "讀卡機連接的 Windows 串列埠號碼",   "COM3"],
            ["Baud Rate",  "串列通訊鮑率，需與讀卡機型號一致",  "115200"],
        ],
        [4, 9, 3]
    )
    heading3("ABB 機械手（SeqNo RAPID 變數位址）")
    body("用於設定系統寫入生產序號（SeqNo）的 RAPID 變數目標位置，需與機器人程式中的變數名稱一致。")
    simple_table(
        ["設定項","說明","預設值"],
        [
            ["Task",     "RAPID Task 名稱",                        "T_ROB1"],
            ["Module",   "RAPID Module 名稱",                      "MES"],
            ["Variable", "RAPID 變數名稱（接收序號的整數變數）",   "MES_project"],
        ],
        [3.5, 9, 3.5]
    )
    note("修改 RAPID 變數位址後，需確認機器人程式中對應的變數已宣告為整數型別（num 或 dnum）", kind="warn")

    heading2("6.9 日誌管理與上傳")
    body("系統設定頁的「日誌管理」區塊提供日誌檔案設定與遠端上傳功能，"
         "可協助工程師遠端取得現場 Log 進行異常排查。")
    heading3("日誌設定")
    simple_table(
        ["欄位","說明"],
        [
            ["儲存至檔案",  "啟用後每日操作紀錄會寫入 %LOCALAPPDATA%\\FProductionDashBoard\\Logs\\ 目錄"],
            ["保留天數",    "超過此天數的舊日誌會自動移至 archive 子目錄"],
        ],
        [4, 12]
    )
    heading3("上傳日誌至遠端")
    step(1, "從下拉選單選擇要上傳的日誌檔案（依日期降冪排列，最新在最上方）")
    step(2, "（選用）點擊「新增附件」加入相關圖片或文件（PNG / JPG / Excel / PDF）")
    step(3, "點擊「上傳」按鈕，系統會將日誌與附件壓縮為 ZIP 後上傳至 IIS 伺服器")
    step(4, "上傳成功後顯示「已上傳：report_YYYYMMDD_HHmmss.zip」並清空附件清單")
    note("上傳期間進度條會顯示處理中狀態；若上傳失敗會顯示錯誤訊息，可重試", kind="info")
    note("附件可重複新增多個，同一檔案不會重複加入；點擊清單右側 ✕ 可移除個別附件", kind="tip")
    img_placeholder("日誌管理區塊（設定 + 上傳 + 附件清單 + 進度條）")
    doc.add_page_break()

    # ══════════════════════════════════════
    # 7. 硬體設定（讀卡機 / Modbus TCP 設備）
    # ══════════════════════════════════════
    heading1("7. 硬體設定（讀卡機 / Modbus TCP 設備）")
    body("點擊左側導覽「設備管理」進入硬體設定，可新增 / 移除讀卡機與 Modbus TCP 設備。")
    img_placeholder("讀卡機設定頁面（讀卡機清單 + 新增按鈕）")

    heading2("7.1 新增讀卡機")
    step(1, "點擊「新增」按鈕")
    step(2, "從 COM Port 下拉選單選擇讀卡機連接的串列埠（預設 COM3）")
    step(3, "確認 Baud Rate（預設 115200，依讀卡機型號設定）")
    step(4, "點擊「測試連線」確認讀卡機可正常通訊")
    step(5, "確認無誤後點擊「套用」儲存設定")
    img_placeholder("新增讀卡機對話框（COM Port 選擇 + 測試按鈕）")
    note("若測試連線失敗，請確認讀卡機 USB 線是否插穩，或確認 COM Port 號碼是否正確", kind="warn")

    heading2("7.2 移除讀卡機")
    step(1, "在讀卡機清單中選取要移除的項目")
    step(2, "點擊「刪除」按鈕並確認")

    heading2("7.3 Modbus TCP 設備")
    body("點擊左側導覽「設備管理」後切換至「Modbus TCP」頁籤，可新增、測試與移除 Modbus TCP 設備連線。")

    heading3("7.3.1 新增 Modbus TCP 連線")
    step(1, "點擊「新增」按鈕，開啟設備設定對話框")
    step(2, "輸入設備名稱與 IP 位址（格式：xxx.xxx.xxx.xxx）")
    step(3, "確認 TCP Port（預設 502，通常無需修改）")
    step(4, "輸入 Unit ID（Modbus 從站地址，範圍 0–255）")
    step(5, "點擊「套用」儲存，設備卡片將自動嘗試建立連線（燈號轉綠代表成功）")
    img_placeholder("Modbus TCP 新增設備對話框（IP / Port / Unit ID 欄位）")
    note("Unit ID 對應 Modbus 從站地址（Slave Address），需與 PLC 或設備的設定一致", kind="info")

    heading3("7.3.2 讀寫測試平台")
    body("Modbus TCP 頁籤提供即時讀寫測試功能，可在不開啟設備卡片的情況下直接測試暫存器。")
    simple_table(
        ["欄位","說明"],
        [
            ["設備選擇",  "從下拉選單選擇要測試的 Modbus 設備"],
            ["起始位址",  "要讀取或寫入的暫存器起始位址（十進位）"],
            ["數量",      "連續讀取的暫存器數量"],
            ["資料格式",  "選擇解析格式（Int16 / UInt16 / Int32_BE / Float32_BE 等）"],
            ["寫入值",    "寫入暫存器的值（讀取測試時留空）"],
        ],
        [4, 12]
    )
    step(1, "選擇目標設備與暫存器位址")
    step(2, "點擊「讀取」取得當前值，或填入寫入值後點擊「寫入」")
    step(3, "結果顯示於下方結果欄位")
    note("讀寫測試平台僅供工程師調試使用，請確認暫存器位址正確後再執行寫入操作", kind="warn")
    img_placeholder("Modbus TCP 讀寫測試平台（位址 / 格式 / 讀寫按鈕 / 結果顯示）")

    heading3("7.3.3 移除 Modbus TCP 設備")
    step(1, "在 Modbus TCP 清單中選取要移除的設備")
    step(2, "點擊「刪除」並確認，設備連線將立即中斷並從清單移除")
    doc.add_page_break()

    # ══════════════════════════════════════
    # 8. 離線模式說明
    # ══════════════════════════════════════
    heading1("8. 離線模式說明")
    body("當系統偵測到與資料庫的連線中斷時，自動切換至離線模式。")
    img_placeholder("離線模式提示（狀態列顯示「離線」 + 橙色指示燈）")

    heading2("8.1 離線模式下可執行的操作")
    for op in ["物料更換","首件檢查","例行巡檢","調試操作（帶點 / 調品質）"]:
        bullet(op)
    body("上述操作在離線模式下會先暫存至本機資料庫（SQLite），"
         "系統同時顯示「已暫存，待連線恢復後自動上傳」提示。")

    heading2("8.2 連線恢復後自動同步")
    body("系統每隔固定時間自動檢測連線狀態，恢復連線後：")
    step(1, "系統自動偵測連線已恢復")
    step(2, "將所有暫存操作依序上傳至資料庫")
    step(3, "狀態列顯示「已上傳 N 筆暫存資料」確認訊息")
    note("請勿在離線期間關閉系統，以免暫存資料尚未同步", kind="warn")
    doc.add_page_break()

    # ══════════════════════════════════════
    # 9. 登出與閒置保護
    # ══════════════════════════════════════
    heading1("9. 登出與閒置保護")

    heading2("9.1 手動登出")
    step(1, "點擊右下角狀態列的「登出」按鈕（或右側使用者圖示）")
    step(2, "系統顯示登出倒計時確認畫面")
    step(3, "確認後系統回到登入畫面")
    img_placeholder("登出確認畫面（倒計時 + 確認/取消按鈕）")

    heading2("9.2 閒置自動登出")
    body("系統自動偵測使用者的滑鼠與鍵盤輸入，當「真實的閒置時間」"
         "（無任何鍵盤滑鼠操作）超過設定值（預設 600 秒），會自動顯示登出倒計時提示。"
         "使用者只要在倒數結束前進行任何操作或點擊「延長登入」即可繼續保持登入狀態。")
    note("系統使用 Windows GetLastInputInfo API 偵測 OS 級別輸入，"
         "因此切換到其他視窗工作也算「未閒置」，不會誤觸發登出。", kind="info")
    img_placeholder("閒置倒計時提示畫面（延長登入 / 確認登出按鈕）")
    note("刷員工卡也可直接進行換手操作，不需先登出再登入", kind="tip")
    doc.add_page_break()

    # ══════════════════════════════════════
    # 10. 常見問題 FAQ
    # ══════════════════════════════════════
    heading1("10. 常見問題（FAQ）")

    faq_items = [
        ("Q: 登入時顯示「帳號或密碼錯誤」",
         "請確認輸入的職號與密碼是否正確（區分大小寫）。"
         "若忘記密碼，請聯繫系統管理員重設。"),

        ("Q: 讀卡機圖示顯示紅色（未連線）",
         "1. 確認讀卡機 USB 線是否插穩\n"
         "2. 確認讀卡機設定中的 COM Port 是否正確\n"
         "3. 嘗試在「設備管理 → 讀卡機」頁面重新套用設定"),

        ("Q: 操作後顯示「已暫存，待連線恢復」",
         "系統目前處於離線模式，操作已記錄至本機。"
         "恢復網路連線後系統會自動上傳，無需手動操作。"),

        ("Q: 設備卡片的時段燈全部顯示灰色",
         "可能原因：巡檢時段設定尚未建立，或設備卡片載入後尚未完成資料更新。"
         "請點擊工具列「更新清單」按鈕重新載入。"),

        ("Q: 想更換語言，但設定選單在哪裡？",
         "語言切換位於登入畫面的右上角下拉選單，需在登入前設定。"
         "登入後無法切換語言，需先登出。"),

        ("Q: 巡檢時段已到，但按巡檢後顯示「目前不在任何巡檢時段內」",
         "請確認目前時間是否在巡檢時段的開始與結束時間範圍內。"
         "若時段設定有跨日，請確認「跨日」選項是否已勾選。"),

        ("Q: 系統載入非常緩慢",
         "請確認電腦可正常連線至公司 SQL Server。"
         "若網路狀況不佳，系統等待連線逾時後會切換至離線模式（約需 1-5 秒）。"),

        ("Q: 刷卡後顯示「未識別卡號，是否查詢 ERP？」",
         "表示此卡號尚未在系統中登記。若為新員工，管理員可選擇查詢 ERP 並新增帳號；"
         "若非本公司員工卡，請勿允許新增。"),

        ("Q: Modbus TCP 設備卡片燈號恆顯示紅色（未連線）",
         "請確認：\n"
         "1. 設備 IP 位址與 Unit ID 是否正確\n"
         "2. 目標設備（PLC / 感測器）已開機且允許 Modbus TCP 連線（TCP 502 port 未被防火牆封鎖）\n"
         "3. 電腦與設備在同一網段，可互相 ping 通\n"
         "若以上皆正常但仍無法連線，可至「設備管理 → Modbus TCP」頁籤進行讀寫測試診斷"),
    ]

    for q, a in faq_items:
        p = doc.add_paragraph()
        p.paragraph_format.space_before = Pt(8)
        p.paragraph_format.space_after  = Pt(2)
        run(p, q, bold=True, size=11, color=C_BLUE)
        p2 = doc.add_paragraph()
        p2.paragraph_format.left_indent = Cm(0.5)
        p2.paragraph_format.space_after = Pt(4)
        run(p2, a, size=10.5)

    doc.add_page_break()

    # ══════════════════════════════════════
    # 附錄：操作快速參考卡
    # ══════════════════════════════════════
    heading1("附錄：操作快速參考卡")
    body("以下為日常最常用操作的快速步驟摘要，可列印後張貼於工作站旁供參考。")
    simple_table(
        ["操作","快速步驟"],
        [
            ["密碼登入",   "①輸入職號 → ②輸入密碼 → ③點擊「登入」"],
            ["刷卡登入",   "①讀卡機連線（狀態燈綠色）→ ②刷員工卡 → ③自動進入"],
            ["物料更換",   "①點擊設備卡片「物料」→ ②選材料 → ③確認"],
            ["首件檢查",   "①點擊「首件」→ ②選正常或異常 → ③確認"],
            ["例行巡檢",   "①點擊「巡檢」→ ②選正常或異常 → ③確認"],
            ["開始調試",   "①點擊「調試」→ ②選帶點或調品質 → 計時開始"],
            ["結束調試",   "①點擊卡片「結束調試」→ ②刷卡或確認 → 記錄上傳"],
            ["更新設備清單","工具列點擊「快速上載」"],
            ["儲存設備清單","工具列點擊「快速儲存」"],
            ["手動登出",   "右下角點擊使用者圖示 → 「登出」"],
        ],
        [4, 12]
    )

    doc.save(DOCX_PATH)
    print(f"✓ Word 操作手冊已生成：{DOCX_PATH}")


# ─────────────────────────────────────────
# PDF
# ─────────────────────────────────────────
def generate_pdf():
    try:
        import win32com.client, pythoncom
        pythoncom.CoInitialize()
        word = win32com.client.Dispatch("Word.Application")
        word.Visible = False
        doc = word.Documents.Open(str(DOCX_PATH))
        doc.SaveAs(str(PDF_PATH), FileFormat=17)
        doc.Close()
        word.Quit()
        print(f"✓ PDF 已生成：{PDF_PATH}")
    except Exception as e:
        print(f"⚠ PDF 生成失敗（{e}），請手動從 Word 匯出")


if __name__ == "__main__":
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    print("生成使用者操作手冊…")
    generate_manual()
    generate_pdf()
    print("\n完成！輸出目錄：", OUTPUT_DIR)
