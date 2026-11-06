using UnityEngine;

namespace MMO.Prototype
{
    public sealed class PrototypeHelp : MonoBehaviour
    {
        public PuppetMotor player;
        public ClassSwitcher classSwitcher;
        public SparringArbalester groupArbalester;
        public TowerFloor tower;
        private bool showHelp = true;
        private GUIStyle title, body, objective;
        private void Update() { if (Input.GetKeyDown(KeyCode.F1)) showHelp = !showHelp; }
        private void OnGUI()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
                body = new GUIStyle(GUI.skin.label) { fontSize = 15 };
                objective = new GUIStyle(body) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            }
            float scale = Mathf.Clamp(Screen.height / 900f, 0.85f, 1.6f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            var mage = player != null ? player.Mage : null;
            var ranger = player != null ? player.Ranger : null;
            bool healer = mage != null && mage.isHealer;
            string className = healer ? "SOIGNEUR" : mage != null ? "MAGE" : ranger != null ? "RÔDEUR" : "GUERRIER";
            Label(new Rect(22, 17, 900, 30), className + (tower != null ? " · TOUR 01" : " · BAC À SABLE"), title);
            if (tower != null)
                Label(new Rect(Screen.width / scale - 580f, 19f, 558f, 26f), tower.Objective, objective);
            if (classSwitcher != null)
                Label(new Rect(22, 48, 900, 25), "TAB · Jouer " + classSwitcher.NextClassLabel, body);
            if (showHelp)
            {
                float top = classSwitcher != null ? 74f : 50f;
                Label(new Rect(22, top, 1100, 25), "ZQSD / WASD : bouger   Maj : courir   Espace : sauter   E : esquiver", body);
                if (healer)
                {
                    Label(new Rect(22, top + 24, 1100, 25), "Clic gauche · Trait lumineux   1 · Soin direct   2 · Régénération", body);
                    string allyHelp = mage.practiceAlly != null ? "   X · Cibler l’allié / soi-même" : "";
                    string cleanse = mage.CanPurify ? "   5 · Purification" : "";
                    string revive = mage.HasRevive ? "   6 · Relèvement" : "";
                    Label(new Rect(22, top + 48, 1100, 25), "3 · Égide   4 · Sanctuaire" + cleanse + revive + allyHelp, body);
                }
                else if (mage != null)
                {
                    Label(new Rect(22, top + 24, 1100, 25), "Clic gauche : trait d’arcane   1 : orbe de feu   2 : éclat de givre", body);
                    Label(new Rect(22, top + 48, 1100, 25), "3 : onde arcanique   4 : barrière", body);
                }
                else if (ranger != null)
                {
                    Label(new Rect(22, top + 24, 1100, 25), "Clic gauche : carreau rapide   1 : tir de précision   2 : carreau entravant", body);
                    Label(new Rect(22, top + 48, 1100, 25), "3 : volée en éventail   4 : carreau brise-garde", body);
                }
                else
                {
                    Label(new Rect(22, top + 24, 1100, 25), "Clic gauche : combo / fente en courant / frappe en l'air   C maintenu : frappe lourde", body);
                    Label(new Rect(22, top + 48, 1100, 25), "F maintenu : garde   F juste avant l'impact : parade   Clic après parade : riposte", body);
                    Label(new Rect(22, top + 72, 1100, 25), "1 : estoc   2 : balayage   3 : brise-garde   4 : recul tranchant", body);
                }
                float cameraRow = top + (mage != null || ranger != null ? 72 : 96);
                string duelHelp = healer && mage.practiceAlly != null ? "T · Pause / reprise du duel" : "T : adversaire actif/pause";
                if (healer && groupArbalester != null) duelHelp = "T · Pause / reprise de l’escarmouche";
                if (tower != null) duelHelp = "T · Pause / reprise du combat";
                Label(new Rect(22, cameraRow, 1100, 25), "Clic droit : caméra   Molette : zoom   " + duelHelp + "   R : reset   F1 : aide", body);
                if (player != null && player.targeting != null)
                    Label(new Rect(22, cameraRow + 24, 1100, 25), "Verrouillage automatique au regard   V · Cible suivante", body);
            }
            if (player == null) return;
            float bottom = Screen.height / scale;
            float manaOffset = mage != null ? 25f : 0f;
            Bar(new Rect(22, bottom - 105 - manaOffset, 210, 12), player.Health / (float)player.maxHealth, new Color(0.82f, 0.32f, 0.28f));
            string poison = player.Poison != null && player.Poison.PulsesLeft > 0 ? "   · POISON " + player.Poison.PulsesLeft : "";
            Label(new Rect(242, bottom - 113 - manaOffset, 420, 25), "Vie " + player.Health + " / " + player.maxHealth + poison, body);
            Bar(new Rect(22, bottom - 80 - manaOffset, 210, 12), player.Stamina / player.MaxStamina, new Color(0.70f, 0.79f, 0.39f));
            Label(new Rect(242, bottom - 88 - manaOffset, 260, 25), "Endurance " + Mathf.FloorToInt(player.Stamina) + " / " + player.MaxStamina, body);
            string action = classSwitcher != null && classSwitcher.ShowBlockedMessage ? "Termine l’action au sol pour changer de classe." : player.Action;
            if (tower == null && healer && groupArbalester != null && groupArbalester.EncounterCleared)
                action = "ATTAQUE REPOUSSÉE · R POUR REJOUER";
            Label(new Rect(22, bottom - 52, 1000, 25), action, body);
            if (mage != null)
            {
                Bar(new Rect(22, bottom - 80, 210, 12), mage.Mana / MageCombat.MaxMana, new Color(0.37f, 0.58f, 0.99f));
                Label(new Rect(242, bottom - 88, 260, 25), "Mana " + Mathf.FloorToInt(mage.Mana) + " / " + MageCombat.MaxMana, body);
                string spells = healer
                    ? FormatCooldown("1 Soin", mage.Cooldown(MageSpell.Mend)) + "   " +
                        FormatCooldown("2 Régénération", mage.Cooldown(MageSpell.Renew)) + "   " +
                        FormatCooldown("3 Égide", mage.Cooldown(MageSpell.Aegis)) + "   " + FormatCooldown("4 Sanctuaire", mage.Cooldown(MageSpell.Sanctuary))
                    : FormatCooldown("1 Feu", mage.Cooldown(MageSpell.FireOrb)) + "   " +
                        FormatCooldown("2 Givre", mage.Cooldown(MageSpell.Frost)) + "   " + FormatCooldown("3 Onde", mage.Cooldown(MageSpell.Nova)) + "   " +
                        FormatCooldown("4 Barrière", mage.Cooldown(MageSpell.Barrier));
                if (mage.CanPurify) spells += "   " + FormatCooldown("5 Purification", mage.Cooldown(MageSpell.Purify));
                if (mage.HasRevive) spells += "   " + FormatCooldown("6 Relèvement", mage.Cooldown(MageSpell.Revive));
                Label(new Rect(22, bottom - 27, 1400, 25), spells, body);
                return;
            }
            if (ranger != null)
            {
                Label(new Rect(22, bottom - 27, 1400, 25), FormatCooldown("1 Précision", ranger.Cooldown(RangerShot.Precision)) + "   " +
                    FormatCooldown("2 Entrave", ranger.Cooldown(RangerShot.Pinning)) + "   " + FormatCooldown("3 Volée", ranger.Cooldown(RangerShot.Volley)) + "   " +
                    FormatCooldown("4 Brise-garde", ranger.Cooldown(RangerShot.Sunder)), body);
                return;
            }
            Label(new Rect(22, bottom - 27, 1400, 25), Cooldown("1 Estoc", WarriorMove.Thrust) + "   " + Cooldown("2 Balayage", WarriorMove.Sweep) +
                "   " + Cooldown("3 Brise-garde", WarriorMove.GuardBreaker) + "   " + Cooldown("4 Recul", WarriorMove.Retreat) +
                "   " + Cooldown("Fente", WarriorMove.Lunge) + "   " + Cooldown("Plongeante", WarriorMove.Plunge), body);
            if (player.RiposteReady)
                Label(new Rect(Screen.width / scale * 0.5f - 115, bottom * 0.35f, 400, 30), "RIPOSTE · CLIC GAUCHE", title);
        }
        private string Cooldown(string name, WarriorMove move)
        {
            return FormatCooldown(name, player.CooldownRemaining(move));
        }
        private static string FormatCooldown(string name, float value) => name + (value > 0f ? " " + value.ToString("0.0") + "s" : " prêt");
        private static void Bar(Rect rect, float fill, Color color)
        {
            GUI.color = new Color(0.08f, 0.09f, 0.10f, 0.85f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color; GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        private static void Label(Rect rect, string text, GUIStyle style)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);
            GUI.color = Color.white; GUI.Label(rect, text, style);
        }
    }
}

