package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.Input;
import com.badlogic.gdx.Screen;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.Table;
import com.badlogic.gdx.utils.viewport.ExtendViewport;

public class GameScreen implements Screen {

	private class Move {
		int dx, dy;

		private Move(int dx, int dy) {
			this.dx = dx;
			this.dy = dy;
		}
	}

	private MyGame game;
	private Level level;
	private Player player;
	private GameBoard gameBoard;

	private Stage boardStage;
	private Stage interfaceStage;

	private Label fps;
	private Label levelLabel;
	private Label difficultyLabel;
	private Label stepsLabel;
	private String stepsText;
	private int steps;

	private PauseDialog pauseDialog;
	private WinDialog completeDialogNormal;
	private WinDialog completeDialogOptimal;

	private Move queuedMove;
	private boolean paused;
	private boolean pauseClosed;
	private boolean completed;

	public GameScreen(Level level, MyGame game) {
		this.level = level;
		this.game = game;

		player = new Player(level);

		interfaceStage = new Stage(new ExtendViewport(800, 600));
		boardStage = new Stage(new ExtendViewport(800, 600));
		gameBoard = new GameBoard(level, player, boardStage.getCamera(), game);
		boardStage.addActor(gameBoard);

		Skin skin = new Skin(Gdx.files.internal("uiskin.json"));

		Table rootTable = new Table();
		rootTable.setFillParent(true);
		//rootTable.setDebug(true);
		interfaceStage.addActor(rootTable);

		// Left and right part of the interface.
		Table leftUI = new Table();
		rootTable.add(leftUI).fillY().expand().left().padLeft(10.0f).padTop(10.0f);
		Table rightUI = new Table();
		rootTable.add(rightUI).fillY().right().padRight(15.0f).padBottom(10.0f).width(65.0f);

		// Level number and difficulty labels in top left corner.
		difficultyLabel = new Label(level.getDifficulty().toString(), skin);
		difficultyLabel.setStyle(new Label.LabelStyle(
				game.getBitmapFont("OldStandard-Regular-40"), Color.BLACK));
		leftUI.add(difficultyLabel).top().padTop(-30.0f);
		leftUI.row();
		levelLabel = new Label("Level " + level.getNumber(), skin);
		levelLabel.setStyle(new Label.LabelStyle(
				game.getBitmapFont("OldStandard-Regular-30"), Color.BLACK));
		leftUI.add(levelLabel).expandY().top().left().padTop(-20.0f);

		// FPS counter in the top right corner.
		fps = new Label("FPS: ", skin);
		rightUI.add(fps).expandY().top();
		rightUI.row();

		// Steps label in the bottom right corner.
		stepsLabel = new Label("00", skin);
		stepsLabel.setStyle(new Label.LabelStyle(
				game.getBitmapFont("OldStandard-Regular-60"), Color.BLACK));
		rightUI.add(stepsLabel).bottom().padBottom(-30.0f);
		rightUI.row();
		Label stepsCaption = new Label("steps", skin);
		stepsCaption.setStyle(new Label.LabelStyle(
				game.getBitmapFont("OldStandard-Regular-30"), Color.BLACK));
		rightUI.add(stepsCaption).bottom();

		// Dialog windows for pausing and completing the level.
		pauseDialog = new PauseDialog(skin, this, game);
		completeDialogNormal = new WinDialog(false, skin, this, game);
		completeDialogOptimal = new WinDialog(true, skin, this, game);

		steps = player.getSteps();
	}

	// The render() method is being used as a hook into the game loop.
	@Override
	public void render(float delta) {
		Gdx.gl.glClearColor(0.949f, 0.941f, 0.925f, 1.0f);
		Gdx.gl.glViewport(0, 0, Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT | GL20.GL_DEPTH_BUFFER_BIT);

		fps.setText("FPS: " + Gdx.graphics.getFramesPerSecond());

		// Update steps variable and label.
		if (player.getSteps() != steps) {
			steps = player.getSteps();
			stepsText = Integer.toString(steps);
			if (stepsText.length() == 1)
				stepsLabel.setText("0" + stepsText);
			else
				stepsLabel.setText(stepsText);
		}

		// Process user input and execute game logic.
		processInput();

		// Update game board and interface.
		boardStage.act();
		boardStage.draw();
		interfaceStage.act();
		interfaceStage.draw();
	}

	@Override
	public void show() {
		Gdx.input.setInputProcessor(interfaceStage);
	}

	@Override
	public void dispose() {
		interfaceStage.dispose();
		boardStage.dispose();
	}

	@Override
	public void resize(int width, int height) {
		// This causes problems. Prohibit resizing in the first place?
		/*
		interfaceStage.getViewport().update(width, height, true);
		boardStage.getViewport().update(width, height, true);
		*/
	}

	@Override
	public void hide() {}

	@Override
	public void pause() {}

	@Override
	public void resume() {}

	public void processInput() {
		if (pauseClosed) {
			pauseClosed = false;
			return;
		}

		if (!paused && !completed && gameBoard.isControllable()) {
			if (queuedMove != null && !gameBoard.isOccupied()) {
				player.move(queuedMove.dx, queuedMove.dy);
				gameBoard.movePlayerModel(queuedMove.dx, queuedMove.dy);
				queuedMove = null;
				return;
			}

			if (Gdx.input.isKeyJustPressed(Input.Keys.UP)) {
				if (gameBoard.isOccupied()) {
					queuedMove = new Move(0, 1);
				} else {
					player.move(0, 1);
					gameBoard.movePlayerModel(0, 1);
				}
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.DOWN)) {
				if (gameBoard.isOccupied()) {
					queuedMove = new Move(0, -1);
				} else {
					player.move(0, -1);
					gameBoard.movePlayerModel(0, -1);
				}
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.RIGHT)) {
				if (gameBoard.isOccupied()) {
					queuedMove = new Move(1, 0);
				} else {
					player.move(1, 0);
					gameBoard.movePlayerModel(1, 0);
				}
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.LEFT)) {
				if (gameBoard.isOccupied()) {
					queuedMove = new Move(-1, 0);
				} else {
					player.move(-1, 0);
					gameBoard.movePlayerModel(-1, 0);
				}
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.R)) {
				resetLevel();
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.ESCAPE)) {
				paused = true;
				pauseDialog.show(interfaceStage);
			}
		}

		if (!completed && gameBoard.isCompleted()) {
			if (player.getSteps() <= level.getOptimalSteps())
				completeLevel(true);
			else
				completeLevel(false);
		}
	}

	public void resetLevel() {
		level.reset();
		player.reset();
		gameBoard.reset();
		completed = false;
	}

	public void completeLevel(boolean optimal) {
		completed = true;
		if (optimal)
			completeDialogNormal.show(interfaceStage);
		else
			completeDialogOptimal.show(interfaceStage);

		game.getSaveState().update(
				level.getDifficulty(), level.getNumber(), optimal);

		//game.getSaveState().save();

		System.out.println(game.getSaveState().getSolveState(level.getDifficulty(), level.getNumber()));
	}

	public void setPaused(boolean paused) {
		this.paused = paused;
	}

	public void setPauseClosed(boolean pauseClosed) {
		this.pauseClosed = pauseClosed;
	}

}

