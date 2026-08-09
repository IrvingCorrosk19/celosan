"""Convierte INFORME_CARGA_HORARIOS_QUENA_2026.md a Word premium."""
from __future__ import annotations

import re
from pathlib import Path

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
from docx.shared import Inches, Pt, RGBColor

ROOT = Path(__file__).resolve().parent.parent
MD = ROOT / "INFORME_CARGA_HORARIOS_QUENA_2026.md"
OUT = ROOT / "INFORME_CARGA_HORARIOS_QUENA_2026.docx"

NAVY = RGBColor(0x1B, 0x3A, 0x5C)
ACCENT = RGBColor(0x2E, 0x74, 0xB5)
GRAY = RGBColor(0x55, 0x55, 0x55)
GREEN = RGBColor(0x1E, 0x7D, 0x32)
ORANGE = RGBColor(0xE6, 0x5C, 0x00)
RED = RGBColor(0xC6, 0x28, 0x28)


def set_cell_shading(cell, fill: str):
    tc = cell._tc
    tcPr = tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), fill)
    shd.set(qn("w:val"), "clear")
    tcPr.append(shd)


def add_horizontal_line(doc):
    p = doc.add_paragraph()
    p.paragraph_format.space_before = Pt(6)
    p.paragraph_format.space_after = Pt(6)
    pPr = p._p.get_or_add_pPr()
    pBdr = OxmlElement("w:pBdr")
    bottom = OxmlElement("w:bottom")
    bottom.set(qn("w:val"), "single")
    bottom.set(qn("w:sz"), "6")
    bottom.set(qn("w:space"), "1")
    bottom.set(qn("w:color"), "2E74B5")
    pBdr.append(bottom)
    pPr.append(pBdr)


def parse_table_lines(lines: list[str]) -> list[list[str]]:
    rows = []
    for line in lines:
        if not line.strip().startswith("|"):
            break
        if re.match(r"^\|\s*[-:| ]+\|\s*$", line):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        rows.append(cells)
    return rows


def add_styled_table(doc, rows: list[list[str]], header_fill="1B3A5C"):
    if not rows:
        return
    table = doc.add_table(rows=len(rows), cols=len(rows[0]))
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    for i, row in enumerate(rows):
        for j, text in enumerate(row):
            cell = table.rows[i].cells[j]
            cell.text = ""
            p = cell.paragraphs[0]
            run = p.add_run(re.sub(r"\*\*", "", text).strip("`"))
            run.font.size = Pt(10)
            if i == 0:
                set_cell_shading(cell, header_fill)
                run.bold = True
                run.font.color.rgb = RGBColor(0xFF, 0xFF, 0xFF)
            elif j == len(row) - 1 and re.search(r"\d+\s*%", text):
                run.bold = True
    doc.add_paragraph()


def add_rich_paragraph(doc, text: str, style=None, size=11, bold=False, color=None, align=None):
    p = doc.add_paragraph(style=style)
    if align:
        p.alignment = align
    parts = re.split(r"(\*\*.*?\*\*|`.*?`)", text)
    for part in parts:
        if not part:
            continue
        run = p.add_run()
        if part.startswith("**") and part.endswith("**"):
            run.text = part[2:-2]
            run.bold = True
        elif part.startswith("`") and part.endswith("`"):
            run.text = part[1:-1]
            run.font.name = "Consolas"
            run.font.size = Pt(9)
        else:
            run.text = part
        run.font.size = Pt(size)
        if bold:
            run.bold = True
        if color:
            run.font.color.rgb = color
    return p


def build_docx():
    text = MD.read_text(encoding="utf-8")
    lines = text.splitlines()
    doc = Document()

    # Márgenes
    for section in doc.sections:
        section.top_margin = Inches(0.9)
        section.bottom_margin = Inches(0.9)
        section.left_margin = Inches(1.0)
        section.right_margin = Inches(1.0)

    # Portada
    title = doc.add_paragraph()
    title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = title.add_run("INFORME PREMIUM")
    r.bold = True
    r.font.size = Pt(14)
    r.font.color.rgb = ACCENT

    h = doc.add_paragraph()
    h.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = h.add_run("Carga de Horarios CELOSAM 2026")
    r.bold = True
    r.font.size = Pt(26)
    r.font.color.rgb = NAVY

    sub = doc.add_paragraph()
    sub.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = sub.add_run("Documento QUENA → SchoolManager (Producción)")
    r.font.size = Pt(13)
    r.font.color.rgb = GRAY

    meta = doc.add_paragraph()
    meta.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = meta.add_run("Centro de Educación Laboral Oficial Nocturno San Miguelito\n5 de julio de 2026")
    r.font.size = Pt(11)
    r.font.color.rgb = GRAY

    add_horizontal_line(doc)

    # KPI box
    kpi = doc.add_paragraph()
    kpi.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = kpi.add_run("COBERTURA: 91,5 %  |  302 horarios cargados  |  27 pendientes  |  12 docentes")
    r.bold = True
    r.font.size = Pt(12)
    r.font.color.rgb = NAVY

    doc.add_page_break()

    i = 0
    in_code = False
    code_buf: list[str] = []

    while i < len(lines):
        line = lines[i]

        if line.strip().startswith("```"):
            if in_code:
                cp = doc.add_paragraph()
                cp.paragraph_format.left_indent = Inches(0.3)
                for cl in code_buf:
                    run = cp.add_run(cl + "\n")
                    run.font.name = "Consolas"
                    run.font.size = Pt(9)
                code_buf = []
                in_code = False
            else:
                in_code = True
            i += 1
            continue

        if in_code:
            code_buf.append(line)
            i += 1
            continue

        if line.strip() == "---":
            add_horizontal_line(doc)
            i += 1
            continue

        if line.startswith("# "):
            p = doc.add_heading(line[2:].strip(), level=0)
            for run in p.runs:
                run.font.color.rgb = NAVY
            i += 1
            continue

        if line.startswith("## "):
            p = doc.add_heading(line[3:].strip(), level=1)
            for run in p.runs:
                run.font.color.rgb = ACCENT
            i += 1
            continue

        if line.startswith("### "):
            title_text = line[4:].strip()
            p = doc.add_heading(title_text, level=2)
            for run in p.runs:
                if title_text.startswith("✅"):
                    run.font.color.rgb = GREEN
                elif title_text.startswith("⚠️"):
                    run.font.color.rgb = ORANGE
                elif title_text.startswith("❌"):
                    run.font.color.rgb = RED
                else:
                    run.font.color.rgb = NAVY
            i += 1
            continue

        if line.startswith("#### "):
            doc.add_heading(line[5:].strip(), level=3)
            i += 1
            continue

        if line.strip().startswith("|"):
            tbl_lines = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                tbl_lines.append(lines[i])
                i += 1
            add_styled_table(doc, parse_table_lines(tbl_lines))
            continue

        if re.match(r"^\d+\.\s", line.strip()):
            p = doc.add_paragraph(style="List Number")
            add_rich_paragraph_content(p, line.strip())
            i += 1
            continue

        if line.strip().startswith("*") and line.strip().endswith("*") and not line.strip().startswith("**"):
            p = doc.add_paragraph()
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            run = p.add_run(line.strip().strip("*"))
            run.italic = True
            run.font.size = Pt(9)
            run.font.color.rgb = GRAY
            i += 1
            continue

        if line.strip():
            add_rich_paragraph(doc, line.strip())
        i += 1

    # Pie de página simple
    doc.add_page_break()
    fp = doc.add_paragraph()
    fp.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = fp.add_run("CELOSAM — SchoolManager | Informe confidencial de operaciones")
    r.font.size = Pt(9)
    r.font.color.rgb = GRAY

    doc.save(OUT)
    print(f"Generado: {OUT}")
    print(f"Tamaño: {OUT.stat().st_size // 1024} KB")


def add_rich_paragraph_content(p, text: str):
    parts = re.split(r"(\*\*.*?\*\*|`.*?`)", text)
    for part in parts:
        if not part:
            continue
        run = p.add_run()
        if part.startswith("**") and part.endswith("**"):
            run.text = part[2:-2]
            run.bold = True
        elif part.startswith("`") and part.endswith("`"):
            run.text = part[1:-1]
            run.font.name = "Consolas"
        else:
            run.text = part
        run.font.size = Pt(10)


if __name__ == "__main__":
    build_docx()
