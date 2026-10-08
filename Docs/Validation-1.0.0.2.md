# GuidingAnalyzer 1.0.0.2 — version à valider dans N.I.N.A.

## Corrections

- Polar Alignment : les champs Site latitude et Screw sensitivity transmettent leur texte immédiatement. Recalculate valide puis applique les deux paramètres ensemble et ne déclenche qu'un calcul.
- Saisie indépendante de la langue de Windows : **point décimal uniquement**. Latitude de −90 à +90 ; sensibilité strictement positive. Pas de conversion silencieuse ni de résultats anciens présentés comme valides après une saisie incorrecte.
- Reconstruction : erreur de position instantanée moins déplacements de guidage déjà appliqués ; aucune intégration des positions. Le pulse d'une image agit sur l'image suivante. Dithers et images perdues sont exclus des mesures.
- Calcul polaire : modèle de dérive DEC avec vitesse sidérale et géométrie corrigées ; intégration de l'angle horaire pendant la mesure ; résolution SVD ; parité DEC courante pour traiter les changements de côté du méridien. Les erreurs sont des déplacements azimut/altitude de l'axe ; les directions affichées sont les **corrections à effectuer**.
- Les positions absentes sont distinctes de zéro. Une ancienne position de calibration n'est pas substituée au pointage de la session. Les sessions sans position ou parité exploitable sont exclues avec une explication.
- Séparation angulaire totale du pôle : `sqrt((az*cos(latitude))² + alt²)`. Deux sessions ne donnent plus un résidu nul présenté comme une validation indépendante.
- FFT : normalisation par le gain de la fenêtre de Hann, indépendante du remplissage par des zéros ; suppression d'un débordement entier sur les logs longs ; échantillonnage uniforme du plus long segment continu, sans franchir les dithers ou les grandes interruptions. Les modes sont nommés Corrections removed et Guided residuals.
- Même calcul spectral dans l'analyse principale et la comparaison de sessions. La durée utilisée pour la confiance de la période de vis correspond au segment effectivement analysé.
- Un résultat d'analyse précédent ne remplace plus la dernière session sélectionnée. La compilation ne déploie plus automatiquement dans N.I.N.A.
- Version DLL/projet/manifest synchronisée ; liens de métadonnées GitHub corrigés.

- Drift Correction : suppression de la dérive uniquement sur RA, ajustée sur le temps réel. Le graphique DEC reste identique lorsque cette option change. Dans les FFT, DEC conserve également sa dérive linéaire ; seule sa moyenne est retirée.
- FFT : spectre complet en fréquence (mHz) et liste des pics dominants classés par contribution énergétique. Chaque ligne affiche fréquence, période, amplitude, RMS estimée, part de puissance et rapport au fond spectral. Sélection : au moins 1 % de puissance, amplitude au moins cinq fois le fond spectral médian et deux cycles observés. Les lobes voisins sont regroupés pour éviter les doublons liés au remplissage par des zéros.
- Ces contributions sont estimées sur le segment analysé avec la fenêtre de Hann. Elles ne prouvent pas une cause mécanique ; l'absence de pic sélectionné ne signifie pas l'absence d'erreur, notamment pour une dérive non périodique.

## Vérification automatisée

Le 8 octobre 2026 : compilation Release réussie et **31 tests réussis, aucun échec**. Le test de saisie charge le template du panneau N.I.N.A., sous langue française, puis invoque le bouton du véritable contrôle WPF. Le plugin n'a pas été exécuté dans une instance N.I.N.A. pour cette vérification.

```powershell
dotnet restore GuidingAnalyzer.sln
dotnet test Tests/GuidingAnalyzer.Tests.csproj -c Release --no-restore -p:DeployPluginToNina=false
dotnet build GuidingAnalyzer.csproj -c Release --no-restore -p:DeployPluginToNina=false
./scripts/Package-Plugin.ps1
```

Les tests utilisent le véritable contrôle WPF et son bouton via UI Automation, sans afficher de fenêtre N.I.N.A. Ils vérifient les saisies sous culture française, l'application des deux valeurs, le rejet des virgules et la récupération après erreur. Ils couvrent aussi les changements rapides de session, les déplacements de guidage, les interruptions, l'amplitude FFT avec 60 000 points et un modèle indépendant de rotation 3D avec erreurs polaires connues dans les deux hémisphères.

Ces tests ne remplacent pas une validation avec les logs des utilisateurs et le plugin chargé dans N.I.N.A. La reconstruction représente une estimation : calibration, backlash, flexure, limites des pulses et modifications de configuration dans une session peuvent affecter les déplacements effectifs. Le calcul polaire exige au moins deux segments de 120 secondes avec 30 images valides chacun, positions et parité connues, et des angles horaires suffisamment différents.

Le choix du plus long segment continu privilégie la validité temporelle ; une session fréquemment interrompue peut ne plus suffire à identifier une longue période de vis. La sensibilité unique des vis reste une approximation : les mécanismes azimut et altitude peuvent avoir des sensibilités différentes.

## Essai utilisateur avant publication

1. Fermer N.I.N.A. et sauvegarder le dossier du plugin existant.
2. Extraire **tout le contenu** de `GuidingAnalyzer-v1.0.0.2-preview.zip` dans `%LOCALAPPDATA%\NINA\Plugins\3.0.0\GuidingAnalyzer\`.
3. Démarrer N.I.N.A. et vérifier que la version chargée est 1.0.0.2.
4. Charger un log utilisateur contenant plusieurs sessions. Dans Polar Alignment, changer la latitude en `35.125`, la sensibilité en `15.5`, puis cliquer Recalculate sans cliquer ailleurs auparavant.
5. Vérifier les valeurs appliquées, les estimations et les fractions de tour. La sensibilité change les tours ; elle ne doit pas changer l'erreur angulaire calculée.
6. Essayer `48,8` : une erreur explicite doit apparaître. Revenir à une valeur avec point et recalculer : les résultats doivent redevenir valides.
7. Vérifier les motifs d'exclusion des sessions et comparer les estimations avec une mesure PHD2 de dérive. Contrôler le sens des corrections sur la monture avant de publier les recommandations.
8. Changer rapidement de session, examiner les modes FFT et la comparaison multi-session, puis exporter un résultat.
9. Activer et désactiver RA Drift Correction : seule la courbe RA doit changer. Vérifier les tableaux FFT et leurs amplitudes associées ; le spectre complet reste disponible même sans pic sélectionné.

## Préparer la publication GitHub

Le ZIP preview contient le plugin compilé et ses dépendances : c'est celui à essayer dans N.I.N.A. Le ZIP sources contient le code corrigé. L'ancien ZIP 1.0.0.1 du dépôt contient des sources et ne constitue pas un installateur du plugin.

Après validation, envoyer les modifications du code sur GitHub, puis créer une Release avec les binaires et les notes de version. La préparation locale ne publie rien et ne change pas l'installation N.I.N.A. existante. Le référencement dans le gestionnaire de plugins N.I.N.A. est une étape distincte d'une Release GitHub.

## Références des conventions

- [PHD2 : colonnes de logs et déplacements de guidage](https://openphdguiding.org/man/Trouble_shooting.htm).
- [PHD2 : directions et distances enregistrées par Mount::MoveOffset](https://github.com/OpenPHDGuiding/phd2/blob/master/src/mount.cpp).
- [PHD2 : parité DEC et calibration](https://github.com/OpenPHDGuiding/phd2/blob/master/src/scope.cpp).
- [PHD2 Log Viewer : reconstruction et analyse](https://github.com/agalasso/phdlogview/blob/master/AnalysisWin.cpp).
- [PHD2 : méthode de dérive, azimut au méridien et altitude vers Est/Ouest](https://openphdguiding.org/man/Tutorials.htm).
