﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameController : Photon.PunBehaviour, IPunObservable {
	#region IPunObservable implementation
	void IPunObservable.OnPhotonSerializeView (PhotonStream stream, PhotonMessageInfo info)
	{
		
	}
	#endregion

	private static GameController _instance;

	public static GameController Instance {
		get {
			if (_instance == null) {
				_instance = (GameController)FindObjectOfType (typeof(GameController));
			}
			return _instance;
		}
	}

	private class Result {
		public const int LOSE = -1;
		public const int NEUTRAL = 0;
		public const int WIN = 1;
	}

	public class AudioSourceIndex {
		public const int CORRECT = 0;
		public const int FALSE = 1;
		public const int SPECIAL_FULL = 2;
		public const int SPECIAL_USE = 3;
		public const int WIN = 4;
		public const int LOSE = 5;
	}

	private const int WIN_NEEDED = 3;

	private const float ANNOUNCEMENT_DELAY = 3.0f;

	[SerializeField]
	private float COMBO_MULTIPLIER;

	[SerializeField]
	private const float SPECIAL_BAR_MODIFIER = 0.01f;
	[SerializeField]
	private const float HEALTH_BAR_MODIFIER = 1f;
	[SerializeField]
	private const float DAMAGE_TO_SPECIAL_DIVISOR = 100f;

	[SerializeField]
	private GameObject blockingPanel;

	[SerializeField]
	private Text problemText;
	[SerializeField]
	private Text answerText;
	[SerializeField]
	private Button backspaceButton;
	[SerializeField]
	private Button[] numberButtons;
	[SerializeField]
	private Button[] difficultyButtons;
	[SerializeField]
	private Text comboText;
	[SerializeField]
	private Slider comboTimerSlider;
	[SerializeField]
	private GameObject specialButton;

	[SerializeField]
	private Slider ownSpecialBarSlider;
	[SerializeField]
	private Slider ownHealthBarSlider;

	[SerializeField]
	private Slider opponentSpecialBarSlider;
	[SerializeField]
	private Slider opponentHealthBarSlider;

	[SerializeField]
	private GameObject ownCharacterObject;
	[SerializeField]
	private GameObject opponentCharacterObject;

	[SerializeField]
	private GameObject countDownPanel;

	[SerializeField]
	private GameObject resultPanel;
	[SerializeField]
	private Text resultText;

	[SerializeField]
	private WinCounter ownWinCounter;
	[SerializeField]
	private WinCounter opponentWinCounter;

	[SerializeField]
	private AudioSource audioSource;
	[SerializeField]
	private AudioClip[] audioClips;

	private KeyValuePair<string, int> problemSet;
	private int solution;
	private int difficulty;

	private int combo;
	private float comboTimer;

	private float ownSpecialGauge;
	private float ownHealthGauge;

	private float opponentSpecialGauge;
	private float opponentHealthGauge;

	private Character ownCharacter;
	private Character opponentCharacter;

	private List <Vector3> numberButtonDefaultPositions;

	private bool resultReceived;

	#region Gameplay related

	void Start () {
		ownCharacter = (Character)ownCharacterObject.AddComponent (System.Type.GetType (CharacterHolder.Instance.OwnCharacterName));
		this.photonView.RPC ("assignOpponentCharacter", PhotonTargets.Others, CharacterHolder.Instance.OwnCharacterName);

		// Add onClick listener to all number buttons and get default position of all number buttons
		numberButtonDefaultPositions = new List <Vector3> ();
		foreach (Button button in numberButtons) {
			button.onClick.AddListener (delegate {
				addNumberToAnswer (button.name [9]);
			});
			numberButtonDefaultPositions.Add (button.transform.position);
		}

		// Add onClick listener to all difficulty buttons
		difficultyButtons [Difficulty.EASY].onClick.AddListener (delegate {
			changeDifficulty (Difficulty.EASY);
		});
		difficultyButtons [Difficulty.MEDIUM].onClick.AddListener (delegate {
			changeDifficulty (Difficulty.MEDIUM);
		});
		difficultyButtons [Difficulty.HARD].onClick.AddListener (delegate {
			changeDifficulty (Difficulty.HARD);
		});

		// Initialize combo & comboTimer
		combo = 0;
		comboTimerSlider.maxValue = ownCharacter.getComboTimer ();

		// Initialize own special & health
		ownSpecialGauge = 0;
		ownHealthGauge = ownCharacter.getMaxHp ();
		ownHealthBarSlider.maxValue = ownHealthGauge;
		ownHealthBarSlider.value = ownHealthGauge;
		specialButton.SetActive (false);

		// Initialize difficulty
		difficulty = Difficulty.MEDIUM;

		// Generate problem
		problemSet = ownCharacter.generateProblem (difficulty);
		problemText.text = problemSet.Key;

		// Initialize result received
		resultReceived = false;
	}

	void Update () {
		// Update combo timer
		if (comboTimer > 0) {
			comboTimer -= Time.deltaTime;
			comboTimerSlider.value = comboTimer;
		} else {
			resetCombo ();
		}

		// Animate bars
		AnimateSlider (ownSpecialBarSlider, ownSpecialGauge, SPECIAL_BAR_MODIFIER);
		AnimateSlider (opponentSpecialBarSlider, opponentSpecialGauge, SPECIAL_BAR_MODIFIER);
		AnimateSlider (ownHealthBarSlider, ownHealthGauge, HEALTH_BAR_MODIFIER);
		AnimateSlider (opponentHealthBarSlider, opponentHealthGauge, HEALTH_BAR_MODIFIER);
	}

	void AnimateSlider (Slider slider, float gauge, float modifier) {
		if (slider.value < gauge && slider.value + modifier <= gauge) {
			slider.value += modifier;
		} else if (slider.value > gauge && slider.value - modifier >= gauge) {
			slider.value -= modifier;
		} else {
			slider.value = gauge;
		}
	}

	void addNumberToAnswer (char number) {
		if (answerText.text != "0") {
			answerText.text += number;
		} else {
			answerText.text = "" + number;
		}
	}

	public void backspace () {
		answerText.text = answerText.text.Substring (0, answerText.text.Length - 1);
		if (answerText.text == "") {
			answerText.text = "0";
		}
	}

	public void deleteAnswer () {
		answerText.text = "0";
	}

	void generateNewProblem () {
		problemSet = ownCharacter.generateProblem (difficulty);
		problemText.text = problemSet.Key;
	}

	void changeDifficulty(int difficulty) {
		deleteAnswer ();
		difficultyButtons [this.difficulty].interactable = true;
		this.difficulty = difficulty;
		generateNewProblem ();
		difficultyButtons [difficulty].interactable = false;
	}

	void resetCombo () {
		combo = 0;
		comboText.text = "";
		comboTimer = 0;
		comboTimerSlider.value = 0;
	}

	public void judgeAnswer() {
		if (int.Parse (answerText.text) == problemSet.Value) {
			generateNewProblem ();

			// Play sound effects
			audioSource.PlayOneShot (audioClips[AudioSourceIndex.CORRECT]);

			// Add combo
			++combo;
			comboText.text = "" + combo;
			comboTimer = ownCharacter.getComboTimer ();

			// Increase own special gauge
			ownSpecialGauge += ownCharacter.getSpecialBarIncrease () [difficulty];
			if (ownSpecialGauge >= 1) {
				ownSpecialGauge = 1;
				if (!specialButton.activeSelf) {
					audioSource.PlayOneShot (audioClips [AudioSourceIndex.SPECIAL_FULL]);
				}
				specialButton.SetActive (true);
			}

			// Decrease opponent's health
			float damage = ownCharacter.getDamage () [difficulty] * (1 + combo * COMBO_MULTIPLIER);
			opponentHealthGauge -= damage;

			// Increase opponent's special gauge
			opponentSpecialGauge += damage / DAMAGE_TO_SPECIAL_DIVISOR;
			if (opponentSpecialGauge >= 1) {
				opponentSpecialGauge = 1;
			}

			// Call RPC
			this.photonView.RPC ("modifyOpponentSpecialGauge", PhotonTargets.Others, ownSpecialGauge);
			this.photonView.RPC ("modifyOwnHealthGauge", PhotonTargets.Others, opponentHealthGauge);
			this.photonView.RPC ("modifyOwnSpecialGauge", PhotonTargets.Others, opponentSpecialGauge);
			if (opponentHealthGauge <= 0) {
				resultPanel.SetActive (true);
				this.photonView.RPC ("setResult", PhotonTargets.Others, Result.LOSE);
			}
		} else {
			resetCombo ();

			// Play sound effects
			audioSource.PlayOneShot (audioClips[AudioSourceIndex.FALSE]);
		}
		deleteAnswer ();
	}

	[PunRPC]
	void modifyOpponentSpecialGauge (float specialGauge) {
		opponentSpecialGauge = specialGauge;
	}

	[PunRPC]
	void modifyOwnSpecialGauge (float specialGauge) {
		ownSpecialGauge = specialGauge;
		if (ownSpecialGauge >= 1) {
			if (!specialButton.activeSelf) {
				audioSource.PlayOneShot (audioClips [AudioSourceIndex.SPECIAL_FULL]);
			}
			specialButton.SetActive (true);
		}
	}

	[PunRPC]
	void modifyOwnHealthGauge (float healthGauge) {
		ownHealthGauge = healthGauge;
		if (ownHealthGauge <= 0) {
			resultPanel.SetActive (true);
			this.photonView.RPC ("setResult", PhotonTargets.Others, Result.WIN);
		}
	}

	string getResultText (float healthPercentage) {
		if (healthPercentage > 0.99f) {
			return "PERFECT";
		} else if (healthPercentage < 0.1f) {
			return "GREAT";
		} else {
			return "K.O";
		}
	}

	[PunRPC]
	void setResult (int result) {
		if (result == Result.LOSE) {
			opponentWinCounter.add ();
		} else {
			ownWinCounter.add ();
		}
		if (!resultReceived) {
			resultReceived = true;
			if (result == Result.LOSE) {
				resultText.text = getResultText (opponentHealthGauge / opponentCharacter.getMaxHp ());
			} else {
				resultText.text = getResultText (ownHealthGauge / ownCharacter.getMaxHp ());
			}
		} else {
			resultText.text = "DOUBLE K.O";
		}
			
		if (ownWinCounter.getWinCount () < WIN_NEEDED && opponentWinCounter.getWinCount () < WIN_NEEDED) {
			StopCoroutine (newRound ());
			StartCoroutine (newRound ());
		} else {
			StopCoroutine (announceWinner ());
			StartCoroutine (announceWinner ());
		}
	}
					
	public void useSpecial () {
		audioSource.PlayOneShot(audioClips[AudioSourceIndex.SPECIAL_USE]);
		ownSpecialGauge = 0;
		specialButton.SetActive (false);
		this.photonView.RPC ("modifyOpponentSpecialGauge", PhotonTargets.Others, ownSpecialGauge);
		opponentCharacter.photonView.RPC ("useSpecial", PhotonTargets.Others);
	}

	public Button[] getNumberButtons() {
		return numberButtons;
	}

	public Vector3[] getNumberButtonDeffaultPositions() {
		return numberButtonDefaultPositions.ToArray ();
	}

	[PunRPC]
	public void assignOpponentCharacter (string characterName) {
		opponentCharacter = (Character)opponentCharacterObject.AddComponent (System.Type.GetType (characterName));
		opponentSpecialGauge = 0;
		opponentHealthGauge = opponentCharacter.getMaxHp ();
		opponentHealthBarSlider.maxValue = opponentHealthGauge;
		opponentHealthBarSlider.value = opponentHealthGauge;

		blockingPanel.SetActive (false);
		countDownPanel.SetActive (true);
	}
		
	IEnumerator newRound () {
		yield return new WaitForSeconds (ANNOUNCEMENT_DELAY);

		ownHealthGauge = ownCharacter.getMaxHp ();
		combo = 0;
		comboTimer = 0;

		opponentHealthGauge = opponentCharacter.getMaxHp ();

		resultText.text = "";
		resultPanel.SetActive (false);

		generateNewProblem ();

		countDownPanel.SetActive (true);
	}

	IEnumerator announceWinner () {
		yield return new WaitForSeconds (ANNOUNCEMENT_DELAY);

		if (ownWinCounter.getWinCount () == WIN_NEEDED) {
			if (opponentWinCounter.getWinCount () < WIN_NEEDED) {
				resultText.text = "WIN";
			} else {
				resultText.text = "DRAW";
			}
		} else {
			resultText.text = "LOSE";
		}
	}
		
	#endregion

	#region others

	public override void OnLeftRoom () {
		SceneManager.LoadScene (0);
	}

	public void leaveRoom () {
		PhotonNetwork.LeaveRoom ();
	}

	public override void OnPhotonPlayerDisconnected (PhotonPlayer other) {
		Debug.Log ("OnPhotonPlayerDisconnected()");
		leaveRoom ();
	}
		
	#endregion

}
