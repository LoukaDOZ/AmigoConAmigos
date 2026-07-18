using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;
using BepInEx;

namespace MoreGamesBase
{
    public class Amigo : GameBase
    {
        [Header("References")]
        [SerializeField]
        private AmigoTV tv;

        [SerializeField]
        private AmigoButton[] Buttons;

        [SerializeField]
        private TextMeshPro totalBetText;


        private List<AmigoButton> _selectedButtons = new List<AmigoButton>();

        private readonly static int selectionCount = 7;
        private readonly static int minVal = 0;
        private readonly static int maxVal = 27;
        private readonly static int blueCount = 7;
        private readonly static int yellowCount = 5;

        protected override bool CanGameStart()
        {
            return _selectedButtons.Count == selectionCount;
        }

        public override void TryStartGame(PlayerInteract playerInteract)
        {
            if (CanGameStart())
                StartGame();
        }

        [Server]
        protected override void StartGame()
        {
            if (!NetworkServer.active) return;

            base.StartGame();

            List<int> exclude = new List<int>();
            int[] blues = new int[blueCount];
            int[] yellows = new int[yellowCount];

            for (int i = 0; i < blueCount; i++)
            {
                int v = GetUniqueResult(exclude);
                blues[i] = v;
                exclude.Add(v);
            }

            for (int i = 0; i < yellowCount; i++)
            {
                int v = GetUniqueResult(exclude);
                yellows[i] = v;
                exclude.Add(v);
            }

            tv.ShowResult(blues, yellows);
        }

        public bool SelectButton(AmigoButton button)
        {
            if (_selectedButtons.Contains(button))
            {
                _selectedButtons.Remove(button);
                return false;
            }

            if (_selectedButtons.Count >= selectionCount)
                return false;

            _selectedButtons.Add(button);
            return true;
        }

        private int GetUniqueResult(List<int> exclude)
        {
            System.Random rnd = new System.Random();
            int v = rnd.Next(minVal, maxVal + 1);

            while (exclude.Contains(v))
                v = rnd.Next(minVal, maxVal + 1);

            return v;
        }
    }

    public class AmigoButton : InteractableBase
    {
        [SerializeField]
        private Amigo amigo;

        [SerializeField]
        private MeshRenderer rend;

        [SerializeField]
        private Material selectedMaterial;

        [SerializeField]
        private Material notSelectedMaterial;

        private bool _isSelected;

        private Vector3 _scale;

        protected override void OnAwake()
        {
            base.OnAwake();
            _scale = rend.transform.localScale;
            rend.material = notSelectedMaterial;
        }

        public override void ServerOnInteract(PlayerInteract playerInteract)
        {
            if (!amigo.isPlaying)
            {
                base.ServerOnInteract(playerInteract);
                _isSelected = amigo.SelectButton(this);
                RpcChangeMaterial(_isSelected);
            }
        }

        [ClientRpc]
        public void RpcChangeMaterial(bool isSelected)
        {
            if (!NetworkClient.active) return;

            rend.material = isSelected ? selectedMaterial : notSelectedMaterial;
        }
    }
    public class AmigoResult : NetworkBehaviour
    {
        [SerializeField]
        private MeshRenderer rend;

        [SerializeField]
        private Material selectedBlueMaterial;

        [SerializeField]
        private Material selectedYellowMaterial;

        [SerializeField]
        private Material notSelectedMaterial;

        public void Select(bool blue)
        {
            RpcChangeMaterial(true, blue);
        }

        public void Reset()
        {
            RpcChangeMaterial(false, false);
        }


        [ClientRpc]
        public void RpcChangeMaterial(bool isSelected, bool blue)
        {
            if (!NetworkClient.active) return;

            rend.material = isSelected ? (blue ? selectedBlueMaterial : selectedYellowMaterial) : notSelectedMaterial;
        }
    }
    public class AmigoTV : MonoBehaviour
    {
        [Header("Game Settings")]
        [SerializeField]
        private float revealDelay = 1f;

        [SerializeField]
        private AmigoResult[] Results;

        public void ShowResult(int[] blueResults, int[] yellowResults)
        {
            Debug.Log("Running Game Amigo");

            StartCoroutine(ShowResultRoutine(blueResults, yellowResults));
        }

        private IEnumerator ShowResultRoutine(int[] blueResults, int[] yellowResults)
        {
            foreach (int i in blueResults)
            {
                yield return new WaitForSeconds(revealDelay);
                Results[i].Select(true);
            }

            yield return new WaitForSeconds(revealDelay);

            foreach (int i in yellowResults)
            {
                yield return new WaitForSeconds(revealDelay);
                Results[i].Select(false);
            }
        }

        public void Reset()
        {
            Debug.Log("Reset Game Amigo");
            foreach (AmigoResult r in Results)
                r.Reset();

        }
    }
}

namespace AmigoConAmigosMod
{
    [BepInPlugin("com.Amigo.AmigoConAmigos", "AmigoConAmigos", "1.0.0")]
    [BepInDependency("com.moregames.base", BepInDependency.DependencyFlags.HardDependency)]
    public class AmigoConAmigosPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogInfo("AmigoConAmigos Assembly loaded by BepInEx! Waiting for Base Loader...");
        }
    }
}