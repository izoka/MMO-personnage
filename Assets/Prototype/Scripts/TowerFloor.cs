using UnityEngine;

namespace MMO.Prototype
{
    public sealed class TowerFloor : MonoBehaviour
    {
        public ClassSwitcher classes;
        public SparringOpponent guardian;
        public SparringArbalester arbalester;
        public TowerGate passageGate, stairsGate;

        public enum Stage { Entrance, Guardian, Passage, Skirmish, Stairs, Complete }
        public Stage Progress { get; private set; }
        public bool Paused { get; private set; }
        private TrainingTarget guardianHealth, swordsmanHealth, arbalesterHealth;
        private PuppetMotor lastPlayer;

        public string Objective
        {
            get
            {
                if (Paused && (Progress == Stage.Guardian || Progress == Stage.Skirmish)) return "COMBAT EN PAUSE · T POUR REPRENDRE";
                switch (Progress)
                {
                    case Stage.Entrance: return "I · ENTRE DANS LA SALLE DU GARDIEN";
                    case Stage.Guardian: return "I · BATS LE GARDIEN DU SEUIL";
                    case Stage.Passage: return "PASSAGE OUVERT · REJOINS LA SALLE II";
                    case Stage.Skirmish: return "II · REPOUSSE L’ESCARMOUCHE";
                    case Stage.Stairs: return "PASSAGE OUVERT · MONTE LES MARCHES";
                    default: return "ÉTAGE 01 TERMINÉ · R POUR REJOUER";
                }
            }
        }

        private void Awake()
        {
            guardianHealth = guardian.GetComponent<TrainingTarget>();
            swordsmanHealth = arbalester.melee.GetComponent<TrainingTarget>();
            arbalesterHealth = arbalester.GetComponent<TrainingTarget>();
        }

        private void Start() { ResetFloor(); }

        public void ResetFloor()
        {
            Paused = false;
            Progress = Stage.Entrance;
            passageGate.Close();
            stairsGate.Close();
            ApplyCombatState();
        }

        public void TogglePause()
        {
            if (Progress != Stage.Guardian && Progress != Stage.Skirmish) return;
            Paused = !Paused;
            ApplyCombatState();
        }

        private void Update()
        {
            var player = classes.ActivePlayer;
            if (player == null || player.Health <= 0) return;
            if (lastPlayer != player)
            {
                lastPlayer = player;
                guardian.SetPlayer(player);
            }

            Vector3 position = player.transform.position;
            switch (Progress)
            {
                case Stage.Entrance:
                    if (position.z >= 1.55f) Advance(Stage.Guardian);
                    break;
                case Stage.Guardian:
                    if (guardianHealth.Health != 0) break;
                    passageGate.Raise();
                    Advance(Stage.Passage);
                    break;
                case Stage.Passage:
                    if (position.z >= 17.0f) Advance(Stage.Skirmish);
                    break;
                case Stage.Skirmish:
                    if (!arbalester.EncounterCleared) break;
                    stairsGate.Raise();
                    Advance(Stage.Stairs);
                    break;
                case Stage.Stairs:
                    if (position.z >= 33.2f && position.y >= 0.67f && Mathf.Abs(position.x) <= 1.8f) Advance(Stage.Complete);
                    break;
            }
        }

        private void Advance(Stage stage)
        {
            Progress = stage;
            Paused = false;
            ApplyCombatState();
        }

        private void ApplyCombatState()
        {
            guardianHealth.CanTakeDamage = Progress == Stage.Guardian;
            swordsmanHealth.CanTakeDamage = arbalesterHealth.CanTakeDamage = Progress == Stage.Skirmish;
            SetAttacking(guardian, !Paused && Progress == Stage.Guardian);
            SetAttacking(arbalester.melee, !Paused && Progress == Stage.Skirmish);
        }

        private static void SetAttacking(SparringOpponent opponent, bool active)
        {
            if (opponent.autoAttack == active) return;
            opponent.autoAttack = active;
            opponent.ResetCycle();
        }
    }
}
