using System.Collections;
using System.Collections.Generic;
using UnityEngine;



    [CreateAssetMenu(fileName = "Digimon", menuName = "Digimon/Create New Digimon")]

    public class DigimonBase : ScriptableObject
    {
        [SerializeField] string name_;

        [TextArea]
        [SerializeField] string description_;

        [SerializeField] GameObject prefab_;

        [SerializeField] Animator animator_;

        [SerializeField] DigimonStage stage_;

        [SerializeField] DigimonAttribute atribute_;

        [SerializeField] DigimonPersonalities personalities_;

        [SerializeField] DigimonType type_;

        [SerializeField] DigimonMoves move_;

        [SerializeField] DigimonDrops drops_;

        //Base Stats
        //[SerializeField] int Level;
        [SerializeField] int ATK;
        [SerializeField] int DEF;
        [SerializeField] int HP;
        [SerializeField] int INT;
        [SerializeField] int SP;
        [SerializeField] int SPD;
        [SerializeField] int CAM;
        [SerializeField] int MU;
        [SerializeField] int ABI;

        [SerializeField] List<LearnableMove> learnableMoves_;

        //Properties
        public string Name
        {
            get { return name_; }
        }
        public string Description
        {
            get { return description_; }
        }

        public Animator Animator
        {
            get { return animator_; }
        }

        public GameObject Prefab
        {
            get { return prefab_; }
        }

        public DigimonStage Stage
        {
            get { return stage_; }
        }

        public DigimonAttribute Atribute
        {
            get { return atribute_; }
        }

        public DigimonType Type
        {
            get { return type_; }
        }

        public DigimonMoves Moves
        {
            get { return move_; }
        }

        public DigimonDrops Drops
        {
            get { return drops_; }
        }
        public int Attack
        {
            get { return ATK; }
        }

        public int Defense
        {
            get { return DEF; }
        }

        public int MaxHp
        {
            get { return HP; }
        }

        public int Inteligence
        {
            get { return INT; }
        }

        public int SpecialPoints
        {
            get { return SP; }
        }

        public int SpecialDefense
        {
            get { return SPD; }
        }
        public int Camaraderie
        {
            get { return CAM; }
        }

        public DigimonPersonalities Personalities
        {
            get { return personalities_; }
        }

        public int MemoryUsage
        {
            get { return MU; }
        }

        public int Ability
        {
            get { return ABI; }
        }
        public List<LearnableMove> LearnableMoves
        {
            get { return learnableMoves_; }
        }

    }

    public enum DigimonStage
    {
        Fresh,
        InTraining,
        Rookie,
        Champion,
        Ultimate,
        Mega,
        Armor,
        Hybrid,
        BurstMode,
        Fusion,
        SuperUltimate
    }
    public enum DigimonPersonalities
    {
        Durable,
        Lively,
        Fighter,
        Defender,
        Brainy,
        Nimble,
        Builder,
        Searcher
    }
    public enum DigimonAttribute
    {
        Fire,
        Water,
        Plant,
        Electric,
        Earth,
        Wind,
        Light,
        Dark,
        Neutral
    }
    [System.Serializable]
    public class LearnableMove
    {
        [SerializeField] public MoveBase movebase;
        [SerializeField] public int level;

    }
    public enum DigimonType
    {
        Virus,
        Data,
        Vaccine,
        Free
    }

    [System.Serializable]
    public class DigimonMoves
    {
        public string[] KnownMoves;
        public string[] SelectedMoves;
        public string[] PassiveSkill;
    }
    [System.Serializable]
    public class DigimonDrops
    {
        public string[] Drops;
    }
