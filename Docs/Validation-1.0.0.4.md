# GuidingAnalyzer 1.0.0.4 — RA de 0.2, correction DEC seule

La version 1.0.0.3 avait étendu à tort la correction DEC au RA, hors du périmètre demandé. La version 1.0.0.4 rétablit exactement la reconstruction RA de 1.0.0.2, y compris le signe des corrections et le traitement de la reprise après dithering. Les explications de la version 1.0.0.3 sur une correction commune aux deux axes ne s'appliquent donc plus.

- Premier graphique : mesures PHD2 conservées.
- Second graphique, RA : mêmes points que 1.0.0.2 ; courbe montante sur la session utilisateur de 22:52.
- Second graphique, DEC : correction inverse des pulses conservée comme dans 1.0.0.3 ; sauts du dithering exclus et segments raccordés. Aucune correction de dérive linéaire sur DEC.
- Bouton RA Drift Correction : agit seulement sur RA.
- Les corrections Polar Alignment et les tableaux FFT avec spectre complet sont conservés.

## Vérification du 8 octobre 2026

36 tests portables réussis en Release. Deux cas supplémentaires comparent chaque point RA à une copie figée du code de l'archive 1.0.0.2, avec dithers, images rejetées, distances de correction présentes ou absentes, et taux de guidage connus ou inconnus. Les tests DEC et les tests du véritable contrôle WPF restent réussis.

Rejeu supplémentaire du log utilisateur : 16 sessions, 7 659 images. Comparaison exacte, sans tolérance numérique, avec le code des archives distribuées : chaque position RA égale celle de 1.0.0.2 ; chaque position DEC égale celle de 1.0.0.3. Les raccordements DEC après settling sont continus. Le graphique de la session de 22:52 a été exporté pour inspection visuelle.

Les données complètes du log et les outils de comparaison locaux ne sont pas distribués dans les archives. Seule la référence de code RA de 1.0.0.2 et les tests portables sont inclus dans les sources.

## Installation pour essai dans N.I.N.A.

1. Fermer N.I.N.A. et sauvegarder le dossier du plugin existant.
2. Extraire tout le contenu de `GuidingAnalyzer-v1.0.0.4-preview.zip` dans `%LOCALAPPDATA%\NINA\Plugins\3.0.0\GuidingAnalyzer\`.
3. Relancer N.I.N.A. et vérifier la version 1.0.0.4.
4. Charger le log et la session de 22:52. Sans correction de dérive RA, la courbe RA retrouve celle de 1.0.0.2. Le bouton de correction de dérive ne modifie pas DEC.

La validation automatisée et le rendu WPF ont été effectués hors d'une instance N.I.N.A. Aucune installation ni publication GitHub n'a été réalisée par la préparation locale.
