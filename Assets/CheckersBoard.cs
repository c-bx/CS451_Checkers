using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CheckersBoard : MonoBehaviour {

	public static CheckersBoard Instance { set; get; }

	public Piece[,] pieces = new Piece[8,8];
	public GameObject redPiecePreFab;
	public GameObject blackPiecePreFab;

	private Vector3 boardOffset = new Vector3 (-4.0f, 0f, -4.0f);
	private Vector3 pieceOffset = new Vector3(0.5f, 0f, 0.5f);

	private bool isRed;
	private bool isRedTurn;
	private bool hasKilled;
	private bool gameIsOver;

	private Piece selectedPiece;
	private List<Piece> forcedPieces;

	private Vector2 mouseOver;
	private Vector2 startDrag;
	private Vector2 endDrag;

	private Client client;

	public void Start() {
		isRedTurn = true;
		GenerateBoard();
	}

	private void Update() {
		UpdateMouseOver();

		// if its my turn
		{
			int x = (int)mouseOver.x;
			int y = (int)mouseOver.y;

			if (selectedPiece != null)
				UpdatePieceDrag(selectedPiece);
			
			if (Input.GetMouseButtonDown(0)) 
				SelectPiece(x, y);
			
			if(Input.GetMouseButtonUp(0))
				TryMove((int)startDrag.x,(int)startDrag.y,x,y);
		}
	}

	private void TryMove(int x1, int y1, int x2, int y2) {

		forcedPieces = ScanForPossibleMove();

		startDrag = new Vector2(x1, y1);
		endDrag = new Vector2(x2, y2);
		selectedPiece = pieces[x1, y1];

		// Out of bounds
		if (x2 < 0 || x2 >= pieces.Length || y2 < 0 || y2 >= pieces.Length) {

			if (selectedPiece != null) 
				MovePiece(selectedPiece, x1, y1);
			
			selectedPiece = null;
			startDrag = Vector2.zero;
			return;
		}

		if (selectedPiece != null) {
			if (endDrag == startDrag) {
				MovePiece(selectedPiece, x1, y1);
				startDrag = Vector2.zero;
				selectedPiece = null;
				return;
			}

			if (selectedPiece.ValidMove (pieces, x1, y1, x2, y2)) {
				if (Mathf.Abs (x1 - x2) == 2) {
					Piece p = pieces [(x1 + x2) / 2, (y1 + y2) / 2];
					if (p != null) {
						pieces [(x1 + x2) / 2, (y1 + y2) / 2] = null;
						DestroyImmediate (p.gameObject);
						hasKilled = true;
					}
				}


				if (forcedPieces.Count != 0 && !hasKilled) {
					MovePiece (selectedPiece, x1, y1);
					startDrag = Vector2.zero;
					selectedPiece = null;
					return;
				}

				pieces [x2, y2] = selectedPiece;
				pieces [x1, y1] = null;
				MovePiece (selectedPiece, x2, y2);

				EndTurn ();
			} else {
				MovePiece(selectedPiece, x1, y1);
				startDrag = Vector2.zero;
				selectedPiece = null;
				return;
			}
		}
	}

	public void SelectPiece(int x, int y) {
		// Out of bounds
		if (x < 0 || x >= 8 || y < 0 || y >= 8) {
			return;
		}

		Piece p = pieces[x, y];
		if (p != null && p.isRed == isRed) {
			if (forcedPieces.Count == 0) {
				selectedPiece = p;
				startDrag = mouseOver;
			} else {
				// Look for the piece under our forced pieces list
				if (forcedPieces.Find(fp => fp == p) == null)
					return;

				selectedPiece = p;
				startDrag = mouseOver;
			}
		}
	}

	private void EndTurn() {
		int x = (int)endDrag.x;
		int y = (int)endDrag.y;

		// Promotions
		if (selectedPiece != null)
		{
			if (selectedPiece.isRed && !selectedPiece.isKing && y == 7)
			{
				selectedPiece.isKing = true;
				//    selectedPiece.transform.Rotate(Vector3.right * 180);
				selectedPiece.GetComponentInChildren<Animator>().SetTrigger("FlipTrigger");
			}
			else if (!selectedPiece.isRed && !selectedPiece.isKing && y == 0)
			{
				selectedPiece.isKing = true;
				//    selectedPiece.transform.Rotate(Vector3.right * 180);
				selectedPiece.GetComponentInChildren<Animator>().SetTrigger("FlipTrigger");
			}
		}

		if(client)
		{
			string msg = "CMOV|";
			msg += startDrag.x.ToString() + "|";
			msg += startDrag.y.ToString() + "|";
			msg += endDrag.x.ToString() + "|";
			msg += endDrag.y.ToString();

			client.Send(msg);
		}

		selectedPiece = null;
		startDrag = Vector2.zero;

		if (ScanForPossibleMove(selectedPiece, x, y).Count != 0 && hasKilled)
			return;

		isRedTurn = !isRedTurn;
		hasKilled = false;
		CheckVictory();

		ScanForPossibleMove();
	}

	private List<Piece> ScanForPossibleMove(Piece p, int x, int y)
	{
		forcedPieces = new List<Piece>();

		if (pieces[x, y].IsForceToMove(pieces, x, y))
			forcedPieces.Add(pieces[x, y]);

		return forcedPieces;
	}


	private void UpdatePieceDrag(Piece p) {
		if (!Camera.main) {
			Debug.Log("Unable to Find Main Camera");
			return;
		}

		RaycastHit hit;
		if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 25.0f, LayerMask.GetMask("Board"))) {
			p.transform.position = hit.point + Vector3.up;
		} 
	}

	private void UpdateMouseOver() {
		// If its my turn
		if (!Camera.main) {
			Debug.Log("Unable to Find Main Camera");
			return;
		}

		RaycastHit hit;
		if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, 25.0f, LayerMask.GetMask("Board"))) {
			mouseOver.x = (int)(hit.point.x - boardOffset.x);
			mouseOver.y = (int)(hit.point.z - boardOffset.z);
		} else {
			mouseOver.x = -1;
			mouseOver.y = -1;
		}
	}


	private void CheckVictory() {
		var ps = FindObjectsOfType<Piece>();
		bool hasWhite = false, hasBlack = false;
		for (int i = 0; i < ps.Length; i++) {
			if (ps[i].isRed)
				hasWhite = true;
			else
				hasBlack = true;
		}

		if (!hasWhite)
			Victory(false);
		if (!hasBlack)
			Victory(true);
	}

	private void Victory(bool isWhite) {
		//winTime = Time.time;

		if (isRed)
			//Alert("White player has won!");
		else
			//Alert("Black player has won!");

		gameIsOver = true;
	}

	private void GenerateBoard() {
		// Generate Red Team
		for (int y = 0; y < 3; y++) {

			bool oddRow = (y % 2 == 0);
			for (int x = 0; x < 8; x+=2) {
				// Generate Piece
				GeneratePiece((oddRow)?x:x+1, y);
			}
		}

		// Generate Black Team
		for (int y = 7; y > 4; y--) {

			bool oddRow = (y % 2 == 0);
			for (int x = 0; x < 8; x+=2) {
				// Generate Piece
				GeneratePiece((oddRow)?x:x+1, y);
			}
		}
	}

	private void GeneratePiece(int x, int y) {
		bool isPieceRed = (y > 3) ? false : true;
		GameObject go = Instantiate((isPieceRed)?redPiecePreFab:blackPiecePreFab) as GameObject;
		go.transform.SetParent(transform);
		Piece p = go.GetComponent<Piece>();
		pieces[x, y] = p;
		MovePiece(p, x, y);
	}

	private void MovePiece(Piece p, int x, int y) {
		p.transform.position = (Vector3.right * x) + (Vector3.forward * y) + boardOffset + pieceOffset;
	}
}
