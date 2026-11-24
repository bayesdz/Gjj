#!/usr/bin/env python3
"""Генерирует схему уровней False Paradise (вид сверху) в HTML/SVG."""
import sys

S = 4.8          # пикселей на метр
OX, OY = 40, 40  # смещение начала координат в пикселях
W, H = 1920, 1080

def X(m): return OX + m * S
def Y(m): return OY + m * S

out = []
def add(s): out.append(s)

ZONE = {
    "z1":   ("#232846", "#7c86ff"),
    "gate": ("#16363a", "#4fd1c5"),
    "z2":   ("#3a2020", "#ff7a59"),
    "fort": ("#3f1a1a", "#ff4d4d"),
    "par":  ("#3a3320", "#f5c542"),
}

def rect(x1, y1, x2, y2, zone, rx=6, dash=False):
    f, s = ZONE[zone]
    d = ' stroke-dasharray="8 6"' if dash else ''
    add(f'<rect x="{X(x1)}" y="{Y(y1)}" width="{(x2-x1)*S}" height="{(y2-y1)*S}" '
        f'rx="{rx}" fill="{f}" stroke="{s}" stroke-width="2.5"{d}/>')

def label(x, y, text, size=15, color="#e8e8f0", weight=600, anchor="middle"):
    add(f'<text x="{X(x)}" y="{Y(y)}" font-size="{size}" fill="{color}" font-weight="{weight}" '
        f'text-anchor="{anchor}">{text}</text>')

def dim(x1, y1, x2, y2, text):
    """Размерная подпись."""
    add(f'<text x="{(X(x1)+X(x2))/2}" y="{(Y(y1)+Y(y2))/2}" font-size="11" fill="#8a8fa8" '
        f'text-anchor="middle" font-family="JetBrains Mono">{text}</text>')

# --- маркеры ---
def enemy(x, y, t, color="#ff4d6d", r=11):
    add(f'<circle cx="{X(x)}" cy="{Y(y)}" r="{r}" fill="{color}" stroke="#14161c" stroke-width="2"/>'
        f'<text x="{X(x)}" y="{Y(y)+5}" font-size="13" font-weight="800" fill="#14161c" text-anchor="middle">{t}</text>')

def npc(x, y, t):
    add(f'<circle cx="{X(x)}" cy="{Y(y)}" r="11" fill="#4fd1c5" stroke="#14161c" stroke-width="2"/>'
        f'<text x="{X(x)}" y="{Y(y)+5}" font-size="12" font-weight="800" fill="#14161c" text-anchor="middle">{t}</text>')

def item(x, y, t):
    cx, cy = X(x), Y(y)
    add(f'<polygon points="{cx},{cy-12} {cx+12},{cy} {cx},{cy+12} {cx-12},{cy}" fill="#f5c542" stroke="#14161c" stroke-width="2"/>'
        f'<text x="{cx}" y="{cy+4}" font-size="{12 if len(t)<2 else 10}" font-weight="800" fill="#14161c" text-anchor="middle">{t}</text>')

def cp(x, y, t):
    cx, cy = X(x), Y(y)
    add(f'<line x1="{cx}" y1="{cy+10}" x2="{cx}" y2="{cy-12}" stroke="#7CFC9A" stroke-width="3"/>'
        f'<polygon points="{cx},{cy-12} {cx+14},{cy-7} {cx},{cy-2}" fill="#7CFC9A"/>'
        f'<text x="{cx+3}" y="{cy+24}" font-size="11" font-weight="700" fill="#7CFC9A" text-anchor="middle">{t}</text>')

def trig(x, y, n):
    """Триггер реплики Гласа — синий квадрат с номером."""
    cx, cy = X(x), Y(y)
    add(f'<rect x="{cx-10}" y="{cy-10}" width="20" height="20" rx="4" fill="#3b82f6" stroke="#14161c" stroke-width="2"/>'
        f'<text x="{cx}" y="{cy+5}" font-size="12" font-weight="800" fill="#fff" text-anchor="middle">{n}</text>')

def cage(x, y):
    cx, cy = X(x), Y(y)
    add(f'<rect x="{cx-13}" y="{cy-13}" width="26" height="26" fill="#fff6c8" stroke="#f5c542" stroke-width="2.5"/>')
    for i in (-6, 0, 6):
        add(f'<line x1="{cx+i}" y1="{cy-13}" x2="{cx+i}" y2="{cy+13}" stroke="#a08020" stroke-width="2"/>')

# ================= фон =================
add(f'<rect width="{W}" height="{H}" fill="#101218"/>')
# сетка 4 м
for m in range(0, 400, 4):
    w = 1 if m % 20 else 1.6
    c = "#1a1d27" if m % 20 else "#232737"
    add(f'<line x1="{X(m)}" y1="{Y(0)}" x2="{X(m)}" y2="{Y(170)}" stroke="{c}" stroke-width="{w}"/>')
for m in range(0, 172, 4):
    w = 1 if m % 20 else 1.6
    c = "#1a1d27" if m % 20 else "#232737"
    add(f'<line x1="{X(0)}" y1="{Y(m)}" x2="{X(380)}" y2="{Y(m)}" stroke="{c}" stroke-width="{w}"/>')

# заголовок
add(f'<text x="{X(0)}" y="{Y(4)}" font-size="34" font-weight="800" fill="#f1f1f6">FALSE PARADISE — схема уровней</text>')
add(f'<text x="{X(0)}" y="{Y(9)}" font-size="16" fill="#9aa0b8">Вид сверху · клетка 4 м, жирная линия 20 м · ходьба 4 м/с · один путь, без развилок, кроме тупиков с предметами</text>')

# ================= ЗОНА 1 =================
rect(18, 148, 36, 168, "z1")                  # R1 вход
rect(36, 154, 52, 160, "z1", rx=2)            # C1
rect(52, 144, 74, 168, "z1")                  # R2 первый бой
rect(60, 130, 66, 144, "z1", rx=2)            # C2
rect(52, 108, 74, 130, "z1")                  # R3 нора
rect(38, 112, 52, 122, "z1", rx=4, dash=True) # тупик
rect(74, 116, 88, 122, "z1", rx=2)            # C3
rect(88, 100, 114, 130, "z1")                 # R4 клетка
rect(114, 112, 136, 118, "z1", rx=2)          # выход

label(75, 95, "ЗОНА 1 · ЛОГОВО ГОБЛИНОВ", 20, "#9aa3ff", 800)
label(75, 99.5, "3–4 мин · путь ≈ 140 м · 3 гоблина · 2 предмета", 13, "#8a8fa8", 500)
label(27, 151.5, "Вход", 13); label(63, 147.5, "Первый бой", 13)
label(63, 111.5, "Нора", 13); label(45, 110.5, "Тупик", 12, "#9aa3ff")
label(101, 103.5, "Зал с клеткой", 13)
dim(18, 170, 36, 170, "18×20"); dim(52, 170.5, 74, 170.5, "22×24")
dim(74, 133, 74, 133, "");
add(f'<text x="{X(76)}" y="{Y(132.5)}" font-size="11" fill="#8a8fa8" font-family="JetBrains Mono">22×22</text>')
add(f'<text x="{X(101)}" y="{Y(134)}" font-size="11" fill="#8a8fa8" text-anchor="middle" font-family="JetBrains Mono">26×30</text>')

# ================= ВРАТА =================
rect(136, 94, 178, 136, "gate", rx=14)
rect(178, 112, 196, 118, "gate", rx=2)
# ворота
add(f'<rect x="{X(176)}" y="{Y(108)}" width="{4*S}" height="{14*S}" fill="#4fd1c5"/>')
add(f'<text x="{X(173)}" y="{Y(139.5)}" font-size="12" fill="#4fd1c5" text-anchor="middle">ворота открываются после Стража</text>')
# лагерь эльфов
rect(140, 97, 160, 106, "gate", rx=4, dash=True)
label(157, 88, "ВРАТА ЭЛЬФОВ · ЗАСТАВА", 20, "#4fd1c5", 800)
label(157, 92.3, "2 мин · площадка 42×42 · бой со Стражем · Око (Q)", 13, "#8a8fa8", 500)
label(150, 109, "лагерь эльфов", 12, "#4fd1c5", 500)

# ================= ЗОНА 2: ДЕРЕВНЯ =================
rect(196, 86, 258, 152, "z2", rx=18)
# хижины
for (a, b, c, d) in [(200, 92, 212, 102), (226, 92, 236, 100), (232, 136, 246, 148),
                     (204, 136, 216, 146), (246, 116, 255, 126)]:
    add(f'<rect x="{X(a)}" y="{Y(b)}" width="{(c-a)*S}" height="{(d-b)*S}" rx="3" fill="#4a2a24" stroke="#ff7a59" stroke-width="1.5"/>')
# кузница
add(f'<rect x="{X(208)}" y="{Y(120)}" width="{12*S}" height="{12*S}" rx="3" fill="#5a3020" stroke="#ffb067" stroke-width="2"/>')
label(214, 135.5, "кузница", 12, "#ffb067", 600)
# огороды
add(f'<rect x="{X(222)}" y="{Y(124)}" width="{10*S}" height="{8*S}" fill="#2c3a22" stroke="#7aa35a" stroke-width="1.5" stroke-dasharray="4 3"/>')
label(227, 135, "огороды", 11, "#7aa35a", 500)
label(227, 157, "ЗОНА 2 · ДЕРЕВНЯ ОГРОВ", 20, "#ff7a59", 800)
label(227, 161.3, "2–3 мин · 62×66 · 2 огра-стража · 2 предмета · кузнец (ИИ)", 13, "#8a8fa8", 500)

# дорога в крепость
rect(240, 66, 248, 86, "z2", rx=2)

# ================= КРЕПОСТЬ =================
rect(222, 22, 266, 66, "fort", rx=4)
rect(232, 26, 256, 46, "fort", rx=3)          # тронный зал
# рисунки на стенах
for (a, b, c, d) in [(232.6, 30, 233.6, 42), (254.4, 30, 255.4, 42)]:
    add(f'<rect x="{X(a)}" y="{Y(b)}" width="{(c-a)*S}" height="{(d-b)*S}" fill="#f5c542"/>')
label(244, 49.5, "тронный зал", 12, "#ff9a9a", 500)
label(244, 16, "КРЕПОСТЬ ОГРА-ВОЖДЯ", 20, "#ff4d4d", 800)
label(244, 20.2, "2 мин · 44×44 · главный выбор · вождь (ИИ)", 13, "#8a8fa8", 500)

# ================= ЛЕСТНИЦА И РАЙ =================
# лестница
for i in range(0, 34, 3):
    add(f'<line x1="{X(266+i)}" y1="{Y(31)}" x2="{X(266+i)}" y2="{Y(39)}" stroke="#f5c542" stroke-width="2"/>')
add(f'<rect x="{X(266)}" y="{Y(31)}" width="{34*S}" height="{8*S}" fill="none" stroke="#f5c542" stroke-width="2"/>')
label(283, 45, "лестница к свету", 12, "#f5c542", 600)
label(283, 48.3, "появляется после выбора", 11, "#8a8fa8", 500)

rect(300, 14, 376, 74, "par", rx=16)
add(f'<path d="M {X(300)} {Y(44)} H {X(376)} V {Y(74)-16} Q {X(376)} {Y(74)} {X(376)-16} {Y(74)} H {X(300)+16} Q {X(300)} {Y(74)} {X(300)} {Y(74)-16} Z" fill="#3d1414"/>')
add(f'<line x1="{X(300)}" y1="{Y(44)}" x2="{X(376)}" y2="{Y(44)}" stroke="#ff4d4d" stroke-width="2.5"/>')
add(f'<rect x="{X(300)}" y="{Y(14)}" width="{76*S}" height="{60*S}" rx="16" fill="none" stroke="#f5c542" stroke-width="2.5"/>')
label(338, 9, "ЛОЖНЫЙ РАЙ → АД", 20, "#f5c542", 800)
label(338, 24, "РАЙ", 18, "#f5c542", 800)
label(338, 28.5, "поля, пруд, белый замок, ангелы", 12, "#d8c48a", 500)
label(338, 32, "богиня спускается", 12, "#d8c48a", 500)
label(338, 54, "АД", 18, "#ff4d4d", 800)
label(338, 58.5, "та же сцена: лава, демоны", 12, "#ff9a9a", 500)
label(338, 62, "концовка А или Б → портал", 12, "#ff9a9a", 500)
label(338, 70.5, "сцена Paradise_Test уже готова", 11, "#8a8fa8", 500)

# ================= ПУТЬ ИГРОКА =================
path = [(27, 158), (44, 157), (63, 157), (63, 137), (63, 119), (81, 119), (101, 115),
        (125, 115), (145, 115), (165, 115), (187, 115), (204, 116), (214, 116),
        (226, 108), (238, 104), (244, 96), (244, 76), (244, 56), (244, 40), (266, 35)]
d = "M " + " L ".join(f"{X(a)},{Y(b)}" for a, b in path)
add(f'<path d="{d}" fill="none" stroke="#ffffff" stroke-opacity="0.55" stroke-width="3" stroke-dasharray="2 9" stroke-linecap="round"/>')
# ответвления к предметам
for seg in [[(63, 119), (45, 117)], [(214, 116), (214, 112)], [(226, 108), (239, 140)], [(214, 116), (206, 100)]]:
    d = "M " + " L ".join(f"{X(a)},{Y(b)}" for a, b in seg)
    add(f'<path d="{d}" fill="none" stroke="#f5c542" stroke-opacity="0.45" stroke-width="2" stroke-dasharray="2 7" stroke-linecap="round"/>')

# ================= МАРКЕРЫ =================
# зона 1
cp(23, 162, "CP1"); trig(30, 154, 1)
enemy(66, 152, "Г1"); trig(55, 157, 2)
add(f'<text x="{X(69)}" y="{Y(153)}" font-size="11" fill="#ff9aac">глитч</text>')
enemy(66, 114, "Г2"); item(70, 126, "И"); trig(58, 126, 3)
add(f'<circle cx="{X(55)}" cy="{Y(112)}" r="9" fill="#0b0c12" stroke="#7c86ff" stroke-width="1.5"/>')
item(42, 117, "Р")
enemy(96, 108, "Г3"); cage(108, 122); trig(108, 113, 4)
trig(130, 115, 5)
# врата
cp(140, 119, "CP2"); trig(143, 129, 6)
enemy(162, 118, "С", "#b07cff", 15)
npc(146, 101, "Ст")
for (a, b) in [(170, 104), (172, 126), (154, 102)]:
    add(f'<circle cx="{X(a)}" cy="{Y(b)}" r="6" fill="#4fd1c5" stroke="#14161c" stroke-width="1.5"/>')
trig(156, 130, 7)
# деревня
cp(200, 120, "CP3"); trig(201, 111, 8)
npc(214, 126, "К")
enemy(228, 110, "О1"); enemy(242, 104, "О2")
item(239, 142, "П"); item(206, 97, "Кл")
# крепость
cp(240, 62, "CP4"); trig(250, 60, 9)
npc(244, 34, "В")
add(f'<circle cx="{X(244)}" cy="{Y(28.5)}" r="6" fill="#4fd1c5" stroke="#14161c" stroke-width="1.5"/>')
add(f'<text x="{X(248)}" y="{Y(29.3)}" font-size="11" fill="#4fd1c5">сын</text>')
trig(236, 40, 10)

# ================= ЛЕГЕНДА / СПИСОК =================
LY = 178  # метры
add(f'<rect x="20" y="{Y(LY)-12}" width="{W-40}" height="{H-Y(LY)-8+12}" rx="14" fill="#161925" stroke="#2a2f45"/>')

def col(x, title, color, lines):
    add(f'<text x="{x}" y="{Y(LY)+16}" font-size="17" font-weight="800" fill="{color}">{title}</text>')
    for i, t in enumerate(lines):
        add(f'<text x="{x}" y="{Y(LY)+40+i*19}" font-size="13" fill="#cfd2e0">{t}</text>')

col(44, "ЗОНА 1 — реплики Гласа", "#9aa3ff", [
    "1  z1.enter — обучение управлению",
    "2  z1.goblin_first_seen; z1.first_glitch на Г1",
    "3  z1.item_toy · Р: z1.item_drawing (тупик)",
    "4  z1.cage_shining — открыть клетку (E)",
    "5  z1.exit",
    "spare_first / kill_first — по событию",
])
col(450, "ВРАТА — реплики", "#4fd1c5", [
    "6  gate.enter",
    "7  gate.guardian_appears → gate.disobey",
    "С — Страж, бой вместе с эльфами",
    "gate.guardian_defeated",
    "Ст — старейшина, 3 кнопки",
    "gate.eye_received → Q включается",
])
col(840, "ЗОНА 2 и КРЕПОСТЬ — реплики", "#ff7a59", [
    "8  z2.enter, z2.eye_first_use",
    "К  z2.smith_seen → диалог (ИИ) → talk_done",
    "П  z2.item_letter · Кл: z2.item_cradle",
    "О1, О2: z2.guard_spared — по событию",
    "9  z2.fortress_enter",
    "10 z2.chief_speech → В: диалог (ИИ), выбор",
])
# условные обозначения
lx = 1290
add(f'<text x="{lx}" y="{Y(LY)+16}" font-size="17" font-weight="800" fill="#e8e8f0">ОБОЗНАЧЕНИЯ</text>')
rows = [
    ('<circle cx="{x}" cy="{y}" r="9" fill="#ff4d6d"/>', "враг (Г гоблин, О огр-страж)"),
    ('<circle cx="{x}" cy="{y}" r="9" fill="#4fd1c5"/>', "NPC (Ст, К кузнец, В вождь), эльф"),
    ('<polygon points="{x},{y0} {x1},{y} {x},{y1} {xm},{y}" fill="#f5c542"/>', "предмет для осмотра (+1 милосердие)"),
    ('<rect x="{xm2}" y="{ym2}" width="18" height="18" rx="4" fill="#3b82f6"/>', "триггер реплики Гласа"),
    ('<polygon points="{x},{ym} {x14},{ym5} {x},{y}" fill="#7CFC9A"/>', "чекпоинт: смерть возвращает сюда"),
    ('<line x1="{xm}" y1="{y}" x2="{x1}" y2="{y}" stroke="#fff" stroke-width="3" stroke-dasharray="2 6"/>', "основной путь · жёлтый — к предмету"),
]
for i, (shape, txt) in enumerate(rows):
    x = lx + 12; y = Y(LY) + 36 + i * 21
    s = shape.format(x=x, y=y, y0=y-10, y1=y+10, x1=x+10, xm=x-10, xm2=x-9, ym2=y-9,
                     ym=y-10, ym5=y-5, x14=x+14)
    add(s)
    add(f'<text x="{lx+32}" y="{y+5}" font-size="13" fill="#cfd2e0">{txt}</text>')

svg = "\n".join(out)
html = f"""<!doctype html><html><head><meta charset="utf-8">
<style>html,body{{margin:0;background:#101218}} text{{font-family:Inter,sans-serif}}</style></head>
<body><svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">{svg}</svg></body></html>"""
open(sys.argv[1], "w", encoding="utf-8").write(html)
