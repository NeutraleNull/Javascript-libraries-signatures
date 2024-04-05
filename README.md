# JavaScript-Code-Signature-Generator

Dieses Tool wurde im Rahmen der Projektgruppe "Erkennung von JavaScript-Bibliotheken in gebündeltem Source-Code" erarbeitet.

## Inhaltsverzeichnis

1. [Einleitung](#einleitung)
2. [Aufbau](#Aufbau)
3. [Installation](#installation)
4. [Verwendung](#verwendung)
5. [Klassenübersicht](#Klassenübersicht)
6. [Verwendete Bibliotheken](#Verwendete-Bibliotheken)
7. [Lizenz](#lizenz)

## Einleitung

Kernstück ist sind drei Tools mit denen es möglich ist Bibliotheken zu indexieren und anschließend eine Erkenenung durchzuführen, sowie eine Auswertung auf ihre Qualität anhand des F1 Scores.
Primär wurden hierfür die Signaturverfahren MinHash und SimHash verwendet.
* ****
## Aufbau

Die Solution besteht aus 5 verschiedenen Projekten, im folgenden ein Kurzer überblick:
* ***Infrastructure*** enthält den Code der für die Generierung, Erstellung und Wiedererkennung der Bibliotheken erforderlich ist und bildet das Kernstück der Anwendung mit dem primären Teil der Logik.
* ***PackageAnalyzer*** ist stellt die Console-Application bereit, die die Interaktion mit dem Benutzer durchführt.
* ***PackageDownloader*** stellt analog zu PackageAnalyzer die Interaktion für den Download und Entpackungsvorgang bereit. Auch die primäre Logik ist hier zu finden.
* ***ScoreCalculator*** ist ein simples Tool um die Ergebnisse von PackageDownloader mit den Ground-Truth aus dem Teil der anderen Mitschreiber dieser PG zu vergleichen.
* ***Testproject*** ist eine Testumgebung, um in einem kleineren Rahmen schnell verschiedene Methoden auszuprobieren.
* ****

## Installation

Für dieses Projekt ist sowohl die Installation von Docker, Docker-compose, als auch von .NET8 nötig.
* .NET8 ``https://learn.microsoft.com/de-de/dotnet/core/install/``
* Docker ``https://www.docker.com/``
* Docker-compose ``https://docs.docker.com/compose/``

Installation Script unter Ubuntu und Debian
```
sudo apt-get update
sudo apt-get install docker docker-compose -y
sudo apt-get install dotnet-sdk-8.0 -y
```

## Verwendung

1. Zunächst musss der im Hauptverzeichnis der Postgres Docker-Container mit mit einem Pfad für das Volumen konfiguriert werden. Hierzu in das src Verzeichniss navigieren:
	1. ``cd src``
	2. ``vim docker-compose.yml``
2. Kompilieren der Anwendung für den Release Modus mit ``dotnet build -c Release`` 
3. Starten des Postgres Docker-Containers mit ``docker-compose up -d``
4. Aus den Tools der anderen PG Teilnehmer wird eine ``dataset.line_json.gz`` zur Verfügung gestellt. 
5. Benutzen des Download-Tools:
	1. Das Tool kann in `src/PackageDownloader/bin/Release/net8.0` gefunden werden.
	2. Ausführen mit `./PackageDownloader download --input-file /path/to/dataset.line_json.gz --output-folder /path/to/output/folder --max-version-age 2014-01-01 --parallel-downloads <thread-count> --no-pre-release true --extract false`
	3. Ausführen des Entpackungs-Vorgang: `./PackageDownloader extract --folder /path/to/input/folder/last-time-output --parallel-extractions <threadcount*4-5>` **Achtung!** Ein Datenset über die "1000-most-dependen-upon Packages" benötigt entpackt etwa 500 GB freien Speicher.
1. Benutzen des Extract-Tools:
	1. Das Tool kann in `src/PackageAnalyzer/bin/Release/net8.0` gefunden werden.
	2. Starten des Indexierungsvorgang mit `./PackageAnalyzer extract-features --input-dir /path/to/extracted/packages --parallel-analyzers <roughly threadcount * 2>`. Dieser Vorgang kann je nach Größe Datensets und Leistung des Rechner bis zu 48 Stunden betragen.
	3. Durchsuchen des Datensatzes mit ``./PackageAnalyzer analyze-folders --input-dir /path/to/bundles/ --min-similarity-minhash <double:0-100 suggested:80> --min-similarity-simhash <double:0-100 suggested:98> --minOccurrencesMinhash <int suggested:5> --minOccurrencesSimhash <int suggested:10> --extraction-threshold <int recommanded:150>`` **==Warnung!==** In der aktuellen Implementation wird die gesamte Datenbank in den Arbeitsspeicher geladen. Eine Datenbank kann mit dem oben generierten Datensatz über 60 GB betragen.
## Klassenübersicht

#### FunctionSignatureContext
Stellt eine Datenbankverbindung bereit die mittels LINQ angesprochen werden kann.
Die statische Klasse ``FunctionSignatureContextExtension`` hilft dabei diese Klasse als Dependency Injection (DI) an die Cocona App zu "heften".
#### PackageIndexer
Diese Klasse zerlegt eine Bibliothek in ihre Versionen und verbreitet diese Parallel. Aus jeder Version werden dann die zur Auswertung relevanten JavaScript Dateien geladen und mittels des ``JavascriptFeatureExtractor`` die Features extrahiert. Ein Featureset besteht aus einer Funktion mit einer Liste an extrahierten Features aus dem Funktionskörper.
Anschließend werden zu jeder Funktion ihre Min-und SimHashes mittels der ``MinHashGenerator`` und ``SimHashGenerator`` Klassen erstellt. Diese werden in einem Database Modell Objekt der Klasse ``FunctionSignature`` gespeichert.
Sobald alle Function-Signatures für alle Dateien einer Version berechnet wurden sind, werden diese gemeinsam per Batch insert in die Datenbank geladen.
#### PackageRecognizer
Hier wird ähnlich dem PackageIndexer verfahren mit zwei unterschieden.
Mit ``LoadDataAsync`` wird zunächst die gesamte Datenbank in den Arbeitsspeicher in der Variable ``DataSet`` gespeichert.
Anschließend kann mit ``AnalyseFolderAsync`` eine beliebige Folder übergeben werden, bei der, ähnlich wie beim PackageIndexer, zunächst für alle Dateien ihre Function-Signatures berechnet werden.
Anschließend wird ein Matching dieser Signaturen gegen die Signaturen in dem ``DataSet`` durchgeführt bei denen gegen ihre Ähnlichkeit gematcht wird.
Am Ende werden alle Function-Signatures nach ihren Bibliotheksnamen gruppiert und die wahrscheinlichste Version anhand des Durschnittes der Wahrscheinlichkeit gewählt. Die Version mit dem höchsten Durschnitt der Wahrscheinlichkeit wird als Version gewählt.

#### JavaScriptFeatureExtractor
`JavascriptFeatureExtractor` ist eine Klasse zur Extraktion von Signaturen aus JavaScript-Code. Sie erzeugt und traversiert den Abstract Syntax Tree (AST) der mit ``Acornima``  erzeugt wurde, um Funktionen und ihre Features zu finden.   
  
Die Hauptmethode, `ExtractFeatures`, generiert den AST und startet die rekursive Suche nach Funktionen.   
  
`VisitNode` durchläuft rekursiv den AST, während `NodeDelegationHandler` das Handling der Knoten basierend auf ihrem Typen an entsprechende Handler delegiert.

#### Verwendete Bibliotheken

* Acornima (https://github.com/adams85/acornima), generiert einen AST basierend auf JavaScript Code
* Microsoft.EntityFrameworkCore (https://learn.microsoft.com/de-de/ef/core/) Datenbank ORM
* Npgsql.EntityFrameworkCore.PostgresSQL (https://github.com/npgsql/efcore.pg) Ergänz Entityframework um support für Postgres
* FastHashes (https://github.com/TommasoBelluzzo/FastHashes) Hocheffiziente Bibliothek die verschiedene schnelle nicht-kryptographische Hashverfahren hinzufügt. Unter anderem MurmurHash und HighwayHash.

##### Weitere nicht relevante Bibliotheken
* Spectre.Console, erleichtert Console UI Interaktion, wie durch Progressbars, oder farblicher Hervorhebung.
* Cocona.net, erleichtert das bauen von Consolenprogrammen mit verschiedenen Argumenten und Methoden.

## Lizenz

MIT