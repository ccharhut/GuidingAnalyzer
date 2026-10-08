# GuidingAnalyzer 1.0.0.3 — reconstruction corrigée

## Correction par rapport à 1.0.0.2

Le premier graphique, PHD2 Guiding Errors, conserve les mesures originales. Le second, Reconstructed Mount Curve, reste une estimation sans guidage, avec raccordement des segments après dithering. Aucune droite de dérive n'est retirée au DEC ; le bouton RA Drift Correction agit uniquement sur RA.

Une erreur de signe amplifiait les mouvements après les pulses : les distances correctives demandées par PHD2 étaient soustraites aux erreurs mesurées. La reconstruction ajoute maintenant les corrections antérieures, puisque celles-ci réduisent l'erreur sur l'image suivante. Cette convention remplace l'explication erronée donnée dans les notes de la version 1.0.0.2. Elle concerne les deux axes du second graphique et les analyses utilisant cette reconstruction ; le premier graphique reste identique.

Les pulses du dithering et de son settling sont exclus. Le premier point valide du nouveau segment rejoint le dernier point valide précédent. Les pulses d'une éventuelle image rejetée pendant la reprise ne créent plus de saut au raccordement.

Les corrections Polar Alignment, la saisie au point décimal et les tableaux de fréquences dominantes avec spectre complet de la version précédente sont conservés. Voir les [notes précédentes](Validation-1.0.0.2.md) pour leur détail.

## Vérification du 8 octobre 2026

- 34 tests automatisés réussis en Release. Les tests de simulation de rotation utilisent désormais la convention de correction du log, vérifiée par un extrait de deux images réellement enregistrées.
- Rejeu supplémentaire du log fourni : 16 sessions et 7 659 images ; raccordements DEC après settling continus. Dans la session de 22:52, les reprises aux images 212 et 979 ont un saut nul.
- Vérification du véritable graphique WPF : activation/désactivation de la correction RA conserve les points du premier graphique et tous les points DEC du second. Export visuel du second graphique sur la session utilisateur.

Une reconstruction sans guidage ne doit pas nécessairement être plate. Elle conserve la dérive estimée et le bruit ; la calibration et le backlash peuvent limiter l'estimation des déplacements effectifs. Aucun ajustement de droite DEC ni lissage destiné à rendre la courbe plate n'a été ajouté. Le plugin reste à essayer dans une instance N.I.N.A.

## Installation pour essai

1. Fermer N.I.N.A. et sauvegarder le dossier du plugin existant.
2. Extraire tout le contenu de `GuidingAnalyzer-v1.0.0.3-preview.zip` dans `%LOCALAPPDATA%\NINA\Plugins\3.0.0\GuidingAnalyzer\`.
3. Relancer N.I.N.A. et vérifier la version 1.0.0.3.
4. Charger le même log et la session de 22:52. Comparer le second graphique, puis activer/désactiver RA Drift Correction : DEC reste identique. Vérifier également les deux modes FFT et Polar Alignment, dont les estimations utilisent la reconstruction corrigée.

La préparation des archives ne modifie pas l'installation N.I.N.A. et ne publie rien sur GitHub.

## Sources des conventions de log

- [PHD2 : calcul des distances correctives et des durées de pulse](https://github.com/OpenPHDGuiding/phd2/blob/master/src/mount.cpp).
- [PHD2 : colonnes effectivement enregistrées](https://github.com/OpenPHDGuiding/phd2/blob/master/src/guidinglog.cpp).

Le log complet de l'utilisateur reste dans les fichiers locaux ignorés et n'est pas inclus dans les archives distribuées. Le test portable ne conserve que deux lignes de mesures, avec un en-tête sans nom d'équipement ni coordonnées de pointage.
