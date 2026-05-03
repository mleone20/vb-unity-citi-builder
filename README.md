# City Builder Tool - Documentazione Completa

## 1) Cos'e questo plugin

City Builder Tool e un plugin editor-only per Unity che permette di:

1. Disegnare e modificare una rete stradale come grafo (nodi + segmenti).
2. Rilevare isolati urbani (blocchi) come cicli chiusi del grafo.
3. Assegnare zoning ai blocchi (manuale o procedurale per distanza dal centro).
4. Generare lotti edificabili in modo coerente con strada, zona e orientamento.
5. Spawnare prefab di edifici sui lotti con controllo di fit geometrico.
6. Applicare workflow procedurali completi (rete + blocchi + zoning + lotti).
7. Fare manutenzione della rete (riparazioni, semplificazione, weld nodi, planarizzazione).
8. Modellare il Terrain sotto strade, blocchi e lotti con blending morbido.

Il sistema e basato su dati serializzati in ScriptableObject (CityData), quindi e versionabile e non dipende da una gerarchia runtime complessa.

## 2) Architettura generale

### 2.1 Componenti principali

- CityManager (MonoBehaviour in scena)
	- Fa da facciata operativa per tutte le azioni su nodi, segmenti, blocchi e lotti.
	- Tiene stato editor corrente (modalita, selezioni).
	- Disegna la citta in Scene View tramite CityRenderer.

- CityData (ScriptableObject)
	- Contiene i dati persistenti della citta:
		- nodes, segments, blocks, lots
		- defaultRoadProfile e globalRoadWidth
		- parametri terrain/lotti
	- Espone query utili (nearest node/segment, find block/lot by point, ecc.).

- CityBuilderWindow (EditorWindow)
	- Interfaccia principale a tab:
		- Strade, Blocchi, Zone/Lotti, Edifici, Config, Procedurale, Strumenti, Statistiche.

- CitySceneHandle (SceneView input handler)
	- Gestisce click, selezione, connessione nodi, movimenti handle, editing Bezier.

- AmericanCityGenerator + AmericanCityConfig
	- Motore procedurale per rete stradale e zoning per distanza.
	- Due modalita: Grid e Branching.

- CityBlockDetector
	- Rileva facce/cicli del grafo stradale per ottenere blocchi.

- CityLotGenerator
	- Genera lotti frontage-based (affaccio su strada) o sparse.

- CityBuildingSpawner
	- Spawna prefab sugli appezzamenti e flatten del Terrain.

- CityRoadPlanarizer
	- Spezza segmenti ai crossing geometrici e rende la rete planare.

### 2.2 Asset di configurazione

- ZoneType
	- Nome, colore, altezza edificio, prefab associati.

- RoadProfile
	- Gerarchia strada, larghezza, colore debug, clearance intersezioni.

- AmericanCityConfig
	- Parametri completi di generazione procedurale (raggio, probabilita, ring, snap, cap, ecc.).

## 3) Setup iniziale

1. Crea CityData:
	 - Assets > Create > CityData
	 - Salvataggio consigliato: Assets/BSCCityBuilder/Assets/CityData.asset

2. Crea CityManager in scena:
	 - GameObject > CityBuilder > Create CityManager

3. Assegna CityData al campo City Data del CityManager.

4. Apri tool:
	 - Window > City Builder > City Builder Tool

5. (Consigliato) Setup asset default:
	 - Tools > City Builder > Setup Default Zone Types
	 - Tools > City Builder > Setup Default Road Profiles

## 4) Funzionalita complete per tab

## 4.1 Tab Strade

### Modalita operative

- Idle
	- Selezione nodo/segmento/lotto.
	- Move handle del nodo selezionato.
	- Handle Bezier del segmento selezionato.

- AddNodes
	- Click in scena: aggiunge nodo.
	- Se click vicino a nodo esistente: seleziona nodo esistente (evita duplicati).
	- Shift+click: collega automaticamente l'ultimo nodo con il nuovo/esistente.

- ConnectNodes
	- Primo click: selezione nodo sorgente.
	- Secondo click: crea segmento verso nodo destinazione.

### Interazioni avanzate

- Ctrl+click su nodo: rimozione rapida nodo + segmenti collegati.
- Snap alla griglia opzionale (GridSize configurabile).
- Allineamento al Terrain opzionale per posizione Y dei nodi.

### Terrain strade

- Flatten Terrain Under Roads
	- Applica brush morbido lungo il tracciato di ogni segmento (anche curvo).
	- Campionamento fitto del percorso per stamp multipli.
	- Rispetta larghezza strada e moltiplicatore width.
	- Dopo flatten, riallinea nodi al terrain.

## 4.2 Tab Blocchi

- Modalita CreateBlock per creazione manuale blocco da catena nodi.
- Editor blocchi (gestito da CityBlockEditor):
	- suggerisci blocchi da rete,
	- conferma preview,
	- gestione blocchi esistenti.
- Flatten Terrain sotto blocchi (consolidato).

## 4.3 Tab Zone / Lotti

- Modalita AssignZoning (click su blocco in scena).
- UI zoning e statistiche zoning (CityZoningEditor).
- Generazione lotti per tutti i blocchi.
- Cancellazione lotti:
	- tutti,
	- solo blocco selezionato.
- Flatten Terrain sotto lotti.

## 4.4 Tab Edifici

- Spawn Edifici da ZoneType
	- Usa prefab configurati in ogni ZoneType.
	- Supporta gestione esistenti:
		- ClearExisting,
		- KeepExisting.

- Cancella Edifici Spawnati
	- Rimuove root dedicata CitySpawnedBuildings.

- Parametri terrain lotti
	- lotTerrainFalloff
	- lotTerrainBlendStrength

## 4.5 Tab Configurazione

- Default Road Profile globale del CityData.
- Larghezza globale fallback per segmenti senza profilo.
- Setup rapido asset default (zone e road profile).

## 4.6 Tab Procedurale

- Selezione/creazione AmericanCityConfig.
- Definizione centro citta P0 (manuale o da oggetto selezionato).
- Cap raggio generazione.
- Gestione Zone Rings (add/remove/sort/reset).
- Preset:
	- Reset Default Americani.
	- Preset Gioco (2.4 km) calibrato per utilizzo ludico.

- Parametri rete:
	- griglia principale/locale,
	- depth multiplier,
	- variazione blocchi,
	- seed,
	- numero autostrade,
	- merge threshold.

- Parametri Branching:
	- max segmenti,
	- max generazioni,
	- snap radius,
	- probabilita dritto/diramazione CBD e Suburbs.

- Parametri Alley:
	- enable,
	- profilo,
	- frazione posizione,
	- raggio massimo.

- Mapping RoadProfile:
	- highway,
	- major,
	- local.

- Azioni:
	- Genera Rete Stradale
	- Assegna Zoning Automatico (distanza)
	- Genera Tutto (rete + blocchi + zoning + lotti)

- Async generation (Branching)
	- Eseguita su piu frame via IEnumerator + EditorApplication.update.
	- Barra progresso integrata in finestra.
	- Pulsante Annulla Generazione.

## 4.7 Tab Strumenti

- Aggiorna Profili Strade Esistenti
	- Migra/normalizza width e profili segmenti gia presenti.

- Semplifica Percorsi
	- Rimuove nodi intermedi quasi collineari e fonde segmenti.

- Ripara Collegamenti
	- Pulisce riferimenti corrotti tra nodi e segmenti.

- Salda Nodi Ravvicinati
	- Clustering per distanza, merge nodi, dedup segmenti.

- Analizza Intersezioni Geometriche
	- Report count intersezioni geometriche candidate.

- Planarizza Rete Stradale
	- Inserisce nodi su crossing e spezza segmenti in catena planare.

- Esporta Statistiche (Console)
- Cancella Tutto (reset completo).

## 4.8 Tab Statistiche

- Conteggi correnti:
	- nodi,
	- segmenti,
	- blocchi,
	- lotti.
- Area totale blocchi.

## 5) Algoritmi usati nel plugin

## 5.1 Generazione rete - Modalita Grid (legacy)

Pipeline:

1. Nodo centrale in P0.
2. Autostrade radiali (numero configurabile).
3. Griglia principale ortogonale (major spacing).
4. Strade locali jittered entro localStreetMaxRadius.
5. Vicoli centrali di strip (se abilitati).
6. Planarizzazione incroci.

Caratteristiche:

- Deterministica con seed.
- Geometria regolare e prevedibile.
- Ideale per layout rapidi e densi.

## 5.2 Generazione rete - Modalita Branching (organica)

Principio:

- Crescita iterativa queue-based per segmenti candidati (PendingSegment).
- Priorita per distanza da P0 (viene espanso prima il piu vicino al centro).

Dettagli operativi:

1. Seeding iniziale
	 - Coppie autostradali opposte + assi ortogonali major.

2. Step dinamico
	 - arteryStep = min(majorSpacing, capRadius * 0.2), con minimo 50m.

3. Cap circolare
	 - Segmenti oltre raggio vengono tagliati al bordo.

4. Snap endpoint
	 - snap = max(mergeThreshold, snapRadius).
	 - Se endpoint e vicino a endpoint confermato, aggancia e chiude incrocio.

5. Dedup pending
	 - Scarta semi quasi uguali da stesso start e direzione simile.

6. Regole di branching
	 - Highway:
		 - continua dritta,
		 - al massimo 1 ramo laterale (sinistra o destra casuale).
	 - Major:
		 - probabilita interpolate tra CBD e Suburbs,
		 - jitter direzione fuori CBD,
		 - in CBD puo generare entrambi i lati,
		 - fuori CBD limita a un solo ramo laterale.

7. Limiti di sicurezza
	 - maxBranchSegments
	 - maxBranchGenerations

8. Planarizzazione finale
	 - risoluzione crossing geometrici.

Benefici attuali del Branching:

- Crescita centro -> periferia (evita pattern ad anello vuoto centrale).
- Probabilita slider realmente influenti sull'esito.
- Migliore controllo del caos con branching contenuto.
- Async in editor con progresso visibile.

## 5.3 Planarizzazione rete (CityRoadPlanarizer)

Scopo:

- Rendere planare il grafo quando due segmenti non adiacenti si incrociano in XZ.

Algoritmo:

1. Scansione coppie segmenti O(N^2) (esclude coppie adiacenti).
2. Test intersezione interna (no endpoint).
3. Raccolta split point per segmento.
4. Per ogni segmento coinvolto:
	 - crea/riusa nodi ai punti split,
	 - ordina lungo segmento,
	 - rimuove segmento originale,
	 - crea catena di sottosegmenti,
	 - preserva road profile.

Output:

- Numero segmenti spezzati inserito nei warning del report.

## 5.4 Rilevamento blocchi (CityBlockDetector)

Approccio:

1. Build adiacenza nodo -> vicini.
2. Ordinamento vicini in senso antiorario.
3. Face tracing con regola mano sinistra su archi diretti.
4. Rimozione facce duplicate con firma canonica del ciclo.
5. Scarto faccia esterna (solo se ci sono piu facce).
6. Filtro overlap area via test centroid-in-polygon su blocchi gia accettati.

Risultato:

- Lista poligoni blocco robusta anche su grafi non banali.

## 5.5 Generazione lotti (CityLotGenerator)

Modalita principali:

- Interior/Exterior (frontage)
	- Per ogni edge blocco, ritaglia lotti lungo bordo strada.
	- Front width = footprint prefab allineato.
	- Depth = footprint prefab.
	- Setback dinamico da larghezza strada effettiva.
	- Gap variabile deterministico (Perlin/hash) o override per blocco.
	- Clamp dentro poligono (interior) o fuori area buildabile (exterior).
	- Validazione anti-overlap con SAT 2D.

- Sparse
	- Distribuzione su griglia interna con jitter deterministico.
	- Rotazioni a passi di 90 gradi.
	- Check area buildabile + SAT overlap.

Output per lotto:

- vertici,
- center,
- height,
- prefab index assegnato,
- rotazione spawn assegnata.

## 5.6 Spawn edifici (CityBuildingSpawner)

Regole:

1. Per ogni blocco con zoning valido.
2. Filtra prefab validi e metadata CityBuilderPrefab.
3. Per ogni lotto:
	 - valida geometria,
	 - usa solo prefab assegnato in fase lotto,
	 - verifica fit footprint entro tolleranza,
	 - applica rotazione/posizione compatibili con frontage.
4. Spawna sotto root CitySpawnedBuildings.

Report completo SpawnReport:

- blocchi processati,
- lotti processati,
- edifici spawnati,
- oggetti rimossi,
- blocchi senza zoning,
- blocchi senza prefab,
- lotti invalidi,
- prefab senza metadata,
- lotti out-of-fit.

## 5.7 Terrain flatten

Tre pass dedicati:

- FlattenTerrainUnderRoads
	- Brush lineare lungo campioni del percorso strada.

- FlattenTerrainUnderLots
	- Flatten morbido su poligoni lotto.

- FlattenTerrainUnderBlocksConsolidated
	- Flatten su aree blocchi/lotti con report dedicato.

Ogni pass restituisce report con campi contatori (processati, modificati, invalidi, samples toccati, fuori terrain, ecc.).

## 6) Output prodotti dal plugin

## 6.1 Output dati persistenti

Dentro CityData:

- Lista CityNode con id e posizione.
- Lista CitySegment con endpoint, width, profilo, geometria (Straight/Bezier), control points.
- Lista CityBlock con vertici, zoning, lotIDs, orientation.
- Lista CityLot con vertici, center, height, prefab/rotation assegnati.

## 6.2 Output visuali in Scene View

- Nodi (size adattiva camera-distance).
- Segmenti con spessore e colore profilo.
- Curva Bezier campionata e handle interattivi.
- Blocchi con fill/outline e label ID.
- Edifici/lotti e selezioni evidenziate.
- Overlay stato City Builder attivo.

## 6.3 Output report/dialog

Il plugin mostra dialog e/o report multilinea per:

- Genera Rete Stradale,
- Assegna Zoning,
- Genera Tutto,
- Spawn edifici,
- Flatten roads/lots/blocks,
- Ripara collegamenti,
- Semplifica percorsi,
- Weld nodi,
- Planarizzazione.

Formato tipico report generazione rete (GenerationReport):

- Nodi creati: X
- Segmenti creati: Y
- Blocchi re-zonati: Z (se presente)
- Warning (N):
	- messaggio 1
	- messaggio 2

## 6.4 Output console

- Log operativi per ogni azione principale.
- Export statistiche con conteggi completi.
- Warning su condizioni non valide (es. blocchi senza zoning, segmenti invalidi, ecc.).

## 7) Workflow consigliati

## 7.1 Workflow manuale completo

1. Disegna rete in tab Strade (AddNodes/ConnectNodes).
2. Suggerisci e conferma blocchi in tab Blocchi.
3. Assegna zoning in tab Zone.
4. Genera lotti in tab Zone/Lotti.
5. Spawn edifici in tab Edifici.
6. Rifinitura con Terrain flatten e strumenti manutenzione.

## 7.2 Workflow procedurale completo

1. Configura AmericanCityConfig in tab Procedurale.
2. Scegli Grid o Branching.
3. Premi Genera Tutto.
4. In Branching monitora barra progresso async.
5. Rifinisci con Planarizza/Repair/Simplify/Weld se necessario.
6. Spawn edifici da ZoneType.

## 8) Preset e configurazioni importanti

## 8.1 ResetToAmericanDefaults

- 5 fasce zona (CBD -> Exurbs), scala ampia realistica.

## 8.2 ResetToGameDefaults (preset 2.4 km)

- generationMode = Branching
- maxGenerationRadius = 2400
- majorGridSpacing = 1600
- localStreetSpacing = 100
- maxBranchSegments = 5000
- maxBranchGenerations = 12
- snapRadius = 20
- prob calibrate CBD/Suburbs
- 4 ring zona (CBD, Inner City, Residential, Suburban)

## 9) Performance e limiti pratici

## 9.1 Costo computazionale principale

- Planarizzazione: O(N^2) su coppie segmenti.
- Block detection: costo legato a dimensione grafo e facce candidate.
- Lot generation: cresce con numero blocchi e perimetri.
- Terrain flatten: dipende da risoluzione heightmap e area toccata.

## 9.2 Suggerimenti performance

- Usa cap radius coerente con scala scena.
- Limita maxBranchSegments quando testi parametri.
- Esegui planarizzazione quando necessario, non ad ogni micro modifica.
- Riduci risoluzione/area terrain per test iterativi.

## 9.3 Limiti attuali

- Editor-only (non runtime gameplay system).
- Nessun sistema traffico/AI incluso.
- Generazione edifici dipende dalla qualita metadata prefab.
- Alcune operazioni assumono geometria principalmente in piano XZ.

## 10) Troubleshooting operativo

Problema: non vedo nodi/azioni in scena.
- Verifica CityManager in scena.
- Verifica CityData assegnato.
- Attiva toggle CitySceneHandle (stato ATTIVO in top bar tool).

Problema: non vengono rilevati blocchi.
- Assicurati che la rete contenga cicli chiusi.
- Esegui Ripara Collegamenti e poi riprova detect.

Problema: spawn edifici nullo.
- Verifica zoning assegnato ai blocchi.
- Verifica prefabs in ZoneType.
- Verifica componente CityBuilderPrefab sui prefab.
- Verifica fit footprint rispetto ai lotti generati.

Problema: rete con crossing problematici.
- Esegui Planarizza Rete Stradale.
- Se necessario, Ripara Collegamenti e poi Semplifica Percorsi.

Problema: valori procedurali "non reagiscono" come atteso.
- In Branching controlla maxBranchSegments/maxBranchGenerations.
- Verifica snapRadius e mergeThreshold.
- Verifica differenza probabilita CBD/Suburbs e ring/cap coerenti.

## 11) Struttura file del modulo

Assets/BSCCityBuilder/

- Scripts/
	- AmericanCityConfig.cs
	- CityData.cs
	- CityManager.cs
	- CityNodeSegmentDefines.cs
	- CityBlockDetector.cs
	- CityLotGenerator.cs
	- CityRenderer.cs
	- CityRoadGeometry.cs
	- CityIntersectionUtility.cs

- Editor/
	- CityBuilderWindow.cs
	- CitySceneHandle.cs
	- CityBuilderMenu.cs
	- AmericanCityGenerator.cs
	- CityGeneratorBase.cs
	- CityRoadPlanarizer.cs
	- CityBlockEditor.cs
	- CityZoningEditor.cs
	- CityBuildingSpawner.cs
	- util/editor helpers vari

- Assets/
	- AmericanCityConfig.asset
	- CityData.asset
	- ZoneTypes/*
	- RoadProfiles/*
	- Examples/*

## 12) Stato attuale del plugin

- Modalita manuale completa: pronta.
- Pipeline procedurale completa: pronta.
- Branching con progress async in editor: pronta.
- Tooling manutenzione rete: pronto.
- Spawn prefab + terrain flatten: pronto.

Data aggiornamento documentazione: 3 maggio 2026.
