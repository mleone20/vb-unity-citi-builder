# BSCCityBuilder

Sistema di **generazione procedurale di città** per Unity (HDRP). Crea automaticamente layout urbani completi — rete stradale, isolati, zonizzazione e posizionamento edifici — a partire da ScriptableObject di configurazione.

Compatibile con **Unity 6000.4.0f1** e **HDRP**.

---

## Funzionalità principali

- **Rete stradale procedurale** — griglie americane, strade radiali, curve di Bézier
- **Rilevamento automatico degli isolati** — algoritmo left-hand rule sul grafo stradale
- **Zonizzazione** — anelli concentrici configurabili (Centro, Commerciale, Residenziale, Suburbano, Rurale)
- **Generazione lotti** — frontage-based o sparse con rispetto delle dimensioni impronta degli edifici
- **Spawn edifici** — selezione pesata/deterministica di prefab, appiattimento terreno opzionale
- **Mesh stradali** — generazione di mesh Unity da segmenti (quad-strip + giunzioni)
- **Sistema plugin** — 7 categorie di plugin con interfacce swappable
- **Editor integrato** — finestra tabulata con anteprima scene-view, gizmo, statistiche
- **AI tagging** — classificazione automatica prefab via LLM locale (LM Studio / OpenAI-compatibile)

---

## Requisiti

| Dipendenza | Versione |
|---|---|
| Unity Editor | 6000.4.0f1 |
| HDRP | incluso nel progetto padre |
| .NET | Standard 2.1 (Assembly-CSharp) |

> Il modulo fa parte del progetto Unity padre `DevAmbient1`. Non è distribuito come pacchetto UPM autonomo.

---

## Architettura

Il sistema è strutturato in cinque layer sovrapposti:

```
┌─────────────────────────────────┐
│         Editor Tools            │  Window, Scene Handles, Mesh Builder
├─────────────────────────────────┤
│       Generation Pipeline       │  6 step modulari con plugin swappabili
├─────────────────────────────────┤
│         Management              │  CityManager: API programmatica + Undo
├─────────────────────────────────┤
│         Rendering               │  CityRenderer: Gizmos, LOD, frustum culling
├─────────────────────────────────┤
│         Data Model              │  CityData, CityNode, CitySegment, CityBlock, CityLot
└─────────────────────────────────┘
```

---

## Modello dati

Tutti i dati della città risiedono in un `CityData` (ScriptableObject).

| Tipo | Descrizione |
|---|---|
| `CityNode` | Intersezione stradale (posizione Vector3 + ID segmenti connessi) |
| `CitySegment` | Strada tra due nodi (retta o curva di Bézier) |
| `CityBlock` | Isolato urbano (poligono + zona + lista lotti) |
| `CityLot` | Lotto edificabile (vertici + centro + altezza + prefab assegnato) |

---

## Pipeline di generazione

La pipeline è orchestrata da `CityGenerationPipelineHost` e si articola in 6 passi:

| Passo | Plugin interface | Descrizione |
|---|---|---|
| 1. Rete stradale | `IRoadNetworkGenerationPlugin` | Genera nodi e segmenti (griglia + radiali) |
| 2. Planarizzazione | `IRoadPlanarizationPlugin` | Splitta segmenti che si intersecano |
| 3. Rilevamento isolati | `IBlockDetectionPlugin` | Traccia cicli nel grafo → poligoni isolati |
| 4. Zonizzazione | `IZoningAssignmentPlugin` | Assegna `ZoneType` per distanza dal centro |
| 5. Layout lotti | `ILotLayoutPlugin` | Divide isolati in lotti edificabili |
| 6. Selezione prefab | `ILotSelectionPlugin` | Sceglie il prefab per ogni lotto |

Ogni passo è indipendente e può essere sostituito con un'implementazione custom.

---

## Configurazione

### ZoneType
Asset (`Assets/ZoneTypes/`) che definisce un distretto urbano:
- colore di visualizzazione
- altezza massima edifici
- lista di prefab edificio con pesi
- nome display (es. `"Center"`, `"Residential"`)

### RoadProfile
Asset (`Assets/RoadProfiles/`) che definisce una categoria stradale:
- larghezza carreggiata
- materiali
- raggio di clearance (per evitare sovrapposizioni)
- gerarchia (Autostrada → Via Locale → Vicolo)

### CityConfig

`CityConfig` è la base generica delle configurazioni di città. Il City Builder conserva sul
`CityManager` un riferimento a questa base e non dipende da una sua implementazione concreta.

Le configurazioni incluse sono:

- `AmericanCityConfig`, per griglia, highway radiali e zoning ad anelli;
- `RandomScatterCityConfig`, per città distribuite tramite scattering.

Un nuovo tipo di città può derivare da `CityConfig`, sovrascrivere proprietà comuni come
`PlanarizationMergeTolerance` e fornire i propri parametri serializzati.

---

## Strumenti Editor

Aprire la finestra principale: **Window → BSC City Builder**

| Tab | Funzione |
|---|---|
| **Paths** | Aggiunta/rimozione nodi e segmenti in Scene View |
| **Blocks** | Lancio rilevamento isolati + visualizzazione |
| **Zoning** | Assegnazione zona per isolato |
| **Buildings** | Anteprima prefab e spawn manuale |
| **Procedural Generation** | Esecuzione pipeline completa con progress bar |
| **Tools** | Mesh stradale, appiattimento terreno, utility |
| **Statistics** | Metriche città (nodi, segmenti, isolati, lotti) |

### Interazione Scene View
`CitySceneHandle` gestisce i seguenti shortcut in modalità editing:
- Click sinistro → piazza nodo
- Drag nodo → connetti segmento
- Click isolato → seleziona per zonizzazione

### Prefab Building
Aggiungere il componente **`CityBuilderPrefab`** a ogni prefab edificio:

| Campo | Descrizione |
|---|---|
| `footprintSize` | Dimensione impronta (X=larghezza, Y=profondità) su piano XZ |
| `frontageOffset` | Offset locale del fronte edificio |
| `frontageDirection` | Normale del fronte (per edifici non ortogonali) |
| `autoComputeFromRenderers` | Calcola automaticamente dalle bounding box dei Renderer |

---

## AI Tagging

Il sistema integra un client LLM locale per classificare automaticamente i prefab edificio.

**Prerequisiti**: LM Studio in esecuzione su `http://localhost:11434` con un modello vision.

**Workflow**:
1. Aprire **Window → BSC City Builder AI → Bulk Classifier**
2. Selezionare i prefab da classificare
3. Il sistema cattura uno screenshot del prefab, costruisce un prompt con i `ZoneType` disponibili e invia al modello
4. La risposta JSON (`{ "description": "...", "zoneTypeDisplayNames": [...] }`) viene salvata nei campi `description` e `zoneTypeTags` del componente `CityBuilderPrefab`

**Finestre AI**:
- `LLMClientSettingsWindow` — configura endpoint e modello
- `LLMBulkClassifierWindow` — classifica batch di prefab
- `LLMClientRequestPreviewWindow` — debug request/response

---

## Sistema Plugin

I plugin sono scoperti automaticamente tramite `TypeCache`. Devono avere un costruttore pubblico senza
parametri, implementare il contratto della categoria ed essere decorati con `CityPlugin`:

```csharp
[CityPlugin("acme.roads", "ACME Roads", CityPluginCategory.RoadNetwork,
    "Generatore stradale custom", Version = "1.2.0", Order = 100)]
[CityPluginDependency("bsc.default.planarization", Optional = true)]
public class MyRoadPlugin : IRoadNetworkGenerationPlugin
{
    public CityGenerationReport GenerateRoadNetwork(CityGenerationContext context)
    {
        return new CityGenerationReport();
    }
}
```

`CityGenerationContext` è per-esecuzione: non servono singleton per passare stato tra plugin.
Un process plugin può implementare `ICityPipelineContributor` e restituire una sequenza ordinata di
`ICityPipelineStep`.

### Estensioni dell'editor

- `ICityBuilderToolbarExtension`: comandi nella barra superiore.
- `ICityBuilderPanelExtension`: pannelli aggiuntivi.
- `ICitySceneViewExtension`: handle e overlay nella Scene View.
- `ICityProcessPluginEditorUI`: configurazione del process plugin.
- `ICityConfigEditorTools`: strumenti mostrati nella tab Tools solo quando è attivo il
  `CityConfig` compatibile.

Esempio di strumento specifico:

```csharp
public sealed class MedievalCityTools : ICityConfigEditorTools
{
    public Type ConfigType => typeof(MedievalCityConfig);
    public int Order => 100;
    public string Title => "MEDIEVAL CITY";

    public void DrawTools(CityConfig config, CityManager manager)
    {
        if (GUILayout.Button("Genera mura")) { /* ... */ }
    }
}
```

### Plugin DLL

Le DLL vengono importate normalmente da Unity e accompagnate da un `CityPluginManifest`.
Il loader verifica API, versione semantica, dipendenze, assembly e whitelist delle categorie.
Usare **Tools → City Builder → Refresh External Plugins** dopo una modifica.

### Motori di generazione stradale

La generazione visuale delle strade è separata dai dati della città. Il City Builder converte la rete
in una `RoadNetworkBuildRequest` contenente polilinee campionate, larghezze, profili, materiali e
giunzioni. Un motore esterno deve implementare `IRoadMeshGenerationEngine`:

```csharp
[RoadMeshEngine("acme.external-roads", "External Roads")]
public sealed class ExternalRoadEngine : IRoadMeshGenerationEngine
{
    public RoadMeshBuildResult Build(RoadNetworkBuildRequest request)
    {
        foreach (RoadPathBuildData road in request.paths)
        {
            // Passa road.points, road.width e road.profile all'asset esterno.
        }
        return new RoadMeshBuildResult { succeeded = true };
    }

    public bool Clear(RoadNetworkBuildRequest request)
    {
        // Rimuove solo l'output prodotto da questo motore.
        return true;
    }
}
```

L'engine viene scoperto automaticamente e appare nel menu **Motore strade**. Un'integrazione Editor
può implementare anche `IRoadMeshGenerationEngineEditorUI` per mostrare impostazioni proprie.

Il motore integrato costruisce ribbon con giunti miter limitati, indicizza spazialmente i tratti,
rileva crossing geometrici anche senza nodi condivisi e genera patch di intersezione dedicate.
Incroci con differenza di quota significativa vengono considerati sovrappassi e non vengono uniti.

### Pipeline di layout dei blocchi

Un `BlockLayoutProfile` contiene una sequenza ordinata di asset `BlockLayoutOperation`. Il profilo
puÃ² essere assegnato allo `ZoneType` e sostituito sul singolo `CityBlock`. Le operazioni built-in
sono primitive combinabili (`Frontage`, `Grid Fill`, `Centered Reserved Area`, `Scatter`): concetti
come un quartiere denso o un parco centrale sono quindi preset di asset e non modalitÃ  del core.

Un'integrazione esterna aggiunge operazioni derivando `BlockLayoutOperation`. Il nuovo tipo appare
automaticamente nel menu **+ Operazione** dell'Inspector del profilo:

```csharp
[CreateAssetMenu(menuName = "City Builder/Layout Operations/ACME")]
public sealed class AcmeLayoutOperation : BlockLayoutOperation
{
    public float customParameter = 10f;

    public override void Execute(BlockLayoutOperationContext context)
    {
        if (!CanExecute(context)) return;
        // Aggiungere risultati a context.lots e aree semantiche a
        // context.reservedAreas. Le operazioni successive li vedranno.
    }
}
```

Plugin built-in disponibili:

| Plugin | Classe |
|---|---|
| Block detection | `DefaultBlockDetectionPlugin` |
| Lot layout (frontage) | `DefaultLotLayoutPlugin` |
| Lot selection (pesata) | `DefaultLotSelectionPlugin` |
| Lot selection (random) | `RandomLotSelectionPlugin` |
| American city network | pipeline in `AmericanCityConfig` |

---

## Struttura cartelle

```
BSCCityBuilder/
├── Assets/
│   ├── RoadProfiles/       # RoadProfile ScriptableObject
│   ├── Settings/           # CityConfig ScriptableObject
│   └── ZoneTypes/          # ZoneType ScriptableObject
├── Editor/
│   ├── AI/                 # LLM client e finestre AI
│   ├── Inspectors/         # Custom Inspector (ZoneType, CityBlock, CityBuilderPrefab)
│   ├── Plugins/            # CityGenerationPipelineHost, CityExternalPluginLoader
│   ├── Roads/              # Registry, host e motore superfici stradali
│   ├── Tools/              # CityBuildingSpawner, CityRoadPlanarizer, CitySceneHandle
│   └── Windows/            # CityBuilderWindow e tab
└── Scripts/
    ├── Components/         # CityBuilderPrefab (MonoBehaviour)
    ├── Config/             # AmericanCityConfig, CityPluginSettings
    ├── Core/               # CityData, CityNode, CitySegment, CityBlock, CityLot
    ├── Generation/         # Plugin interfaces e implementazioni default
    ├── Management/         # CityManager
    ├── Plugins/            # CityPluginRegistry, CityPluginManifest
    └── Rendering/          # CityRenderer
```

---

## Note di sviluppo

- Le query sui nodi più vicini usano distanza planare **XZ** (non 3D) per correttezza su terreni inclinati.
- Nei namespace `BSCCityBuilder.Editor.*`, usare `UnityEditor.Editor` qualificato per evitare conflitti tipo-vs-namespace.
- I percorsi degli asset sono ricavati dalla posizione del package tramite `CityBuilderAssetPaths`.
- I manifest delle DLL vengono validati da `CityExternalPluginLoader`; Unity resta responsabile
  dell'import e del caricamento dell'assembly.
