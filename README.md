# Maus-Highlighter

Kleines Windows-Tool, das den Mauszeiger mit einem farbigen Kreis hervorhebt – ideal für Präsentationen,
Bildschirmaufnahmen und Screen-Sharing.

## Funktionen

- **Kreis um den Mauszeiger** – Farbe, Radius, Deckkraft der Füllung und Randstärke einstellbar
- **Klick-Wellen** – bei Linksklick laufen Wellen wie bei einem Stein im Wasser von innen nach außen,
  nur innerhalb des Kreises (Anzahl, Dauer, Linienstärke und Farbe einstellbar)
- **Mehrere Monitore** – funktioniert über alle angeschlossenen Bildschirme, auch bei unterschiedlicher
  Windows-Skalierung (Per-Monitor-DPI)
- **Durchklickbar** – der Kreis blockiert keine Mausklicks
- **Ein/Aus per Tastenkürzel:** `Strg+Alt+H`
- **Tray-Symbol** mit Einstellungen, optional Autostart mit Windows

## Verwendung

1. `MausHighlighter.exe` starten – keine Installation nötig (benötigt das in Windows 10/11 enthaltene .NET Framework 4.x).
2. Rechtsklick (oder Doppelklick) auf das Tray-Symbol → **Einstellungen…**
3. Beenden über Rechtsklick auf das Tray-Symbol → **Beenden**.

Die Einstellungen werden in `%APPDATA%\MausHighlighter\settings.ini` gespeichert.

## Selbst bauen

```bat
build.cmd
```

Nutzt den in Windows enthaltenen C#-Compiler (`csc.exe` aus dem .NET Framework 4.x), es muss nichts installiert werden.

### Entwickler-Optionen

- `MausHighlighter.exe --settings` – öffnet beim Start direkt die Einstellungen
- `MausHighlighter.exe --test-ripple` – löst kurz nach dem Start eine Klick-Welle aus
