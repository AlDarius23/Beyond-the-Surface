using UnityEngine;

namespace BeyondTheSurface
{
    /// <summary>
    /// Holds the player's "personal anchor" data (parents' names, from the Waiting Room questionnaire)
    /// and the behaviour metrics collected during the scenarios.
    /// Names are injected into the hospital chart, monitor and mother's dialogue.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Personal anchor (set by the Waiting Room questionnaire)")]
        public string fatherName = "Ion Popescu";
        public string motherName = "Maria Popescu";
        public int fatherAge = 58;
        public int motherAge = 55;

        [Header("Behaviour metrics (Scenario 1)")]
        public float watchLookSeconds;
        public int angryGestures;
        public bool timerFailed;

        void Awake()
        {
            Instance = this;
            fatherName = PlayerPrefs.GetString("bts_father", fatherName);
            motherName = PlayerPrefs.GetString("bts_mother", motherName);
            fatherAge = PlayerPrefs.GetInt("bts_fatherAge", fatherAge);
            motherAge = PlayerPrefs.GetInt("bts_motherAge", motherAge);
        }

        public void SetNames(string father, string mother, int fAge, int mAge)
        {
            fatherName = father; motherName = mother; fatherAge = fAge; motherAge = mAge;
            PlayerPrefs.SetString("bts_father", father);
            PlayerPrefs.SetString("bts_mother", mother);
            PlayerPrefs.SetInt("bts_fatherAge", fAge);
            PlayerPrefs.SetInt("bts_motherAge", mAge);
            PlayerPrefs.Save();
        }
    }
}
