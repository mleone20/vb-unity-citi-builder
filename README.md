# 🏙️ City Builder Tool - Guida Rapida

## Setup Iniziale (5 minuti)

### 1. Crea Asset CityData
```
Menu: Assets > Create > CityData
Salva in: Assets/BSCCityBuilder/Assets/CityData.asset
```

### 2. Crea CityManager nella Scena
```
Menu: GameObject > CityBuilder > Create CityManager
Oppure: Tasto destro Hierarchy > Create Empty > Aggiungi componente CityManager
```

### 3. Collega CityData a CityManager
```
Seleziona CityManager nell'Hierarchy
Nel componente CityManager (Inspector), trascina CityData.asset nel campo "City Data"
```

### 4. Apri Editor Window
```
Menu: Window > City Builder > City Builder Tool
```

---

## Modalità Operative

### 📍 Modalità: Aggiungi Nodi
- **Seleziona**: Bottone "Aggiungi Nodi" in EditorWindow
- **Azione**: Clicca nella Scene View dove vuoi posizionare un nodo
- **Risultato**: Nodo bianco (cubo piccolo) appare nella scene

### 🔗 Modalità: Connetti Nodi
- **Seleziona**: Bottone "Connetti Nodi"
- **Azione 1**: Click primo nodo → diventa giallo (selezionato)
- **Azione 2**: Click secondo nodo → crea segmento grigio tra loro
- **Ripeti**: Per tracciare altre strade

### 🎨 Modalità: Assegna Zoning
- **Seleziona**: Bottone "Assegna Zoning"
- **Azione**: Click su un blocco → popup scelta zona
- **Opzioni**: Residenziale (verde), Commerciale (blu), Industriale (giallo), Speciale (grigio)

---

## Workflow Completo

### Step 1: Traccia la Rete Stradale
1. Modalità "Aggiungi Nodi" → clicca 4-5 volte per creare rettangolo
2. Modalità "Connetti Nodi" → crea segmenti che racchiudono area (fare un quadrato/rettangolo chiuso)

### Step 2: Rileva Blocchi
1. EditorWindow → sezione "Blocchi"
2. Button "Suggerisci Blocchi" → anteprima area in giallo
3. Button "Conferma Blocchi Suggeriti" → salva blocchi ufficiali

### Step 3: Assegna Zone
1. EditorWindow → sezione "Zoning"
2. Seleziona blocco da dropdown
3. Click su uno dei 4 bottoni zona (es. "Residenziale")
4. Vedi colore blocco aggiornato in Scene View live

### Step 4: Genera Lotti ed Edifici
1. EditorWindow → sezione "Lotti"
2. Adjust slider "Dimensione Media Lotto" (default 30, prova 20-50)
3. Button "Genera Lotti per tutti i Blocchi"
4. **Automatico**: Edifici 3D appaiono attorno al perimetro blocchi, colorati per zona

### Step 5: Regola Parametri Globali
- **Larghezza Strade**: Slider sezione "Impostazioni Strade"
- **Altezze Edifici**: Sezione "Edifici" → 4 slider (Residenziale, Commerciale, Industriale, Speciale)
- **Scala Globale Edifici**: Slider "Building Scale"

---

## Visualizzazione Scene View

| Elemento | Colore | Significato |
|----------|--------|------------|
| Nodo | Bianco/Giallo | Interszione strada (giallo = selezionato) |
| Segmento | Grigio rettangolo | Strada |
| Blocco outline | Nero leggero | Contorno isolato |
| Blocco riempimento | Verde/Blu/Giallo/Grigio | Zona assegnata |
| Edificio cubo | Verde/Blu/Giallo/Grigio | Edificio per zona |

---

## Parametri Principali

### Impostazioni Strade
- **Road Width**: 1.0 - 10.0 (default 3.0) → larghezza strade visualizzazione

### Impostazioni Lotti
- **Avg Lot Size**: 10 - 100 (default 30) → dimensione media lotto, influenza numero edifici/blocco

### Impostazioni Edifici
- **Building Scale**: 0.5 - 2.0 (default 1.0) → scala globale moltiplicativa altezze
- **Height Residential**: default 5.0 m
- **Height Commercial**: default 20.0 m
- **Height Industrial**: default 12.0 m
- **Height Special**: default 8.0 m

---

## Azioni Globali (EditorWindow)

| Azione | Effetto |
|--------|--------|
| **Esporta Statistiche** | Stampa Console: nodi, segmenti, blocchi, lotti count |
| **Cancella Tutto** | Resetta intera città (richiede conferma) |
| **Idle** | Disattiva tutti i modi di interazione |

---

## Tips & Tricks

### ⭐ Workflow Veloce (10 minuti)
1. Aggiungi 4 nodi a forma di rettangolo
2. Connetti in sequenza (n1→n2, n2→n3, n3→n4, n4→n1)
3. Suggerisci Blocchi
4. Conferma
5. Assegna una zona a blocco
6. Genera Lotti
7. Vedi edifici apparire!

### 🔧 Test Algoritmi
- **Rileva Blocchi**: Funziona solo se il grafo forma cicli chiusi
- **Genera Lotti**: Require almeno 1 blocco con minArea > 1.0 m²
- **Edifici**: Appaiono solo se ci sono lotti (quindi blocchi + zoning + genera lotti)

### 📊 Debug
- Button "Esporta Statistiche" → Console mostra dati completi
- Inspector CityData → vedi liste nodì, segmenti, blocchi, lotti

### 🛠️ Backup
- CityData.asset è versionabile Git (non contiene GameObject)
- Se cancelli per errore: Undo (Ctrl+Z)

---

## Troubleshoot

| Problema | Soluzione |
|----------|-----------|
| Nessun nodo appare al click | Verifica CityManager assegnato, verifica Scene View focus |
| Blocchi non si rilevano | Grafo deve essere chiuso (nodi collegati in ciclo) |
| Edifici non appaiono | Genera lotti prima (sezione Lotti) |
| Colori sbagliati | Verifica zoning assegnato correttamente (sezione Zoning) |
| CityManager non trovato | Menu: GameObject > CityBuilder > Create CityManager |
| CityData non assegnato | Menu: Assets > Create > CityData, poi drag-drop in Inspector |

---

## File Component Struttura

```
Assets/BSCCityBuilder/
├── Scripts/
│   ├── CityNodeSegmentDefines.cs
│   ├── CityData.cs
│   ├── CityManager.cs
│   ├── CityRenderer.cs
│   ├── CityBlockDetector.cs
│   └── CityLotGenerator.cs
├── Editor/
│   ├── CityBuilderMenu.cs
│   ├── CitySceneHandle.cs
│   ├── CityBlockEditor.cs
│   ├── CityZoningEditor.cs
│   └── CityBuilderWindow.cs
├── Assets/
│   └── CityData.asset (creato da utente)
└── README.md (this file)
```

---

## Note Tecniche

- ✅ **No GameObject/Prefab istanziati** - Tutto dati + Gizmos
- ✅ **Persistenza versionabile** - CityData asset = file binario serializzato Unity
- ✅ **Editor-only** - Niente export runtime per ora (futura feature)
- ✅ **Unity 6 compatible** - HDRP supportato
- ✅ **Single assembly** - No namespaces, Assembly-CSharp

---

## Supporto

- 🐛 Bug frame-drops? → Riduci numero lotti o blocchi
- 📝 Vuoi aggiungere feature? → Vedi file script per estensione
- 🔄 Reset veloce? → Button "Cancella Tutto" → crea nuovo CityData → riassegna a CityManager

---

**Versione**: 1.0 | **Data**: 2 aprile 2026 | **Status**: Production Ready ✅
