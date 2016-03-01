package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.Input;
import com.badlogic.gdx.Screen;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.graphics.Texture;
import com.badlogic.gdx.graphics.g2d.SpriteBatch;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.Image;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.Table;
import com.badlogic.gdx.scenes.scene2d.utils.Drawable;
import com.badlogic.gdx.utils.viewport.ExtendViewport;

import java.io.IOException;

public class GameScreen implements Screen {

	private class Move {
		int dx, dy;

		private Move(int dx, int dy) {
			this.dx = dx;
			this.dy = dy;
		}
	}

	//public static final float TUT_RESIZE = 1/
	private MyGame game;
	private Settings settings;
	private Level level;
	private Player player;
	private GameBoard gameBoard;

	private Table rootTable;
	private Stage boardStage;
	private Stage interfaceStage;
	private Skin skin;
	private SpriteBatch spriteBatch;

	private Label fps;
	private Label levelLabel;
	private Label difficultyLabel;
	private Label stepsLabel;
	private String stepsText;
	private int steps;
	private TextureRegion tutorial1;

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

		spriteBatch = new SpriteBatch();
		settings = game.getSettings();
		player = new Player(level);
		skin = game.getSkin();

		interfaceStage = new Stage(new ExtendViewport(Gdx.graphics.getWidth(), Gdx.graphics.getHeight()));
		boardStage = new Stage(new ExtendViewport(Gdx.graphics.getWidth(), Gdx.graphics.getHeight()));
		gameBoard = new GameBoard(level, player, boardStage.getCamera(), game);
		boardStage.addActor(gameBoard);

		rootTable = new Table();
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
				game.getBitmapFont("Vollkorn-Regular-58"), Color.BLACK));
		leftUI.add(difficultyLabel).top().left().padTop(-45.0f);
		leftUI.row();
		levelLabel = new Label("Level " + level.getNumber(), skin);
		levelLabel.setStyle(new Label.LabelStyle(
				game.getBitmapFont("Vollkorn-Regular-46"), Color.BLACK));
		leftUI.add(levelLabel).expandY().top().left().padTop(-40.0f);

		// FPS counter in the top right corner.
		fps = new Label("FPS: ", skin);
		fps.setColor(Color.BLACK);
		//rightUI.add(fps).expandY().top();
		rightUI.row();

		// Steps label in the bottom right corner.
		stepsLabel = new Label("00", skin);
		stepsLabel.setStyle(new Label.LabelStyle(
				game.getBitmapFont("OldStandard-Regular-60"), Color.BLACK));
		//rightUI.add(stepsLabel).expandY().bottom().padBottom(-30.0f);
		rightUI.row();
		Label stepsCaption = new Label("steps", skin);
		stepsCaption.setStyle(new Label.LabelStyle(
				game.getBitmapFont("OldStandard-Regular-30"), Color.BLACK));
		//rightUI.add(stepsCaption).bottom();

		// Dialog windows for pausing and completing the level.
		pauseDialog = new PauseDialog(skin, this, game);
		completeDialogNormal = new WinDialog(false, skin, level, this, game);
		completeDialogOptimal = new WinDialog(true, skin, level, this, game);

		steps = player.getSteps();

		if (level.getDifficulty() == Difficulty.NORMAL) {
			if (level.getNumber() == 1) {
				tutorial1 = game.getTutorialTexture(settings.getResolution());
			} else if (level.getNumber() == 2) {
				String button = Input.Keys.toString(settings.getRestartButton());
				Label message = new Label("You can restart the level by pressing "
								+ button + ".", skin);
				message.setStyle(new Label.LabelStyle(
						game.getBitmapFont("Vollkorn-Regular-32"), Color.BLACK));
				leftUI.row();
				leftUI.add(message).left().bottom();
			} else if (level.getNumber() == 3) {
				Label message = new Label("The order in which you match the "
						+ "different colors is irrelevant.", skin);
				message.setStyle(new Label.LabelStyle(
						game.getBitmapFont("Vollkorn-Regular-32"), Color.BLACK));
				leftUI.row();
				leftUI.add(message).left().bottom();
			}
		}

		if (settings.getUIHidden()) {
			rightUI.setVisible(false);
			leftUI.setVisible(false);
		}
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

		// Display tutorial texture if necessary.
		if (!game.getSettings().getUIHidden()
				&& level.getDifficulty() == Difficulty.NORMAL
				&& level.getNumber() == 1) {
			spriteBatch.begin();
			spriteBatch.draw(tutorial1, 0f, 0f);
			spriteBatch.end();
		}
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

		// DEBUGGING: Re-setup level
		if (Gdx.input.isKeyJustPressed(Input.Keys.S))
			game.openLevel(level.getDifficulty(), level.getNumber());
		// DEBUGGING ---------------

		// Only process in-game input if game is (still) running.
		if (!paused && !completed) {
			// Escape to open pause menu.
			if (Gdx.input.isKeyJustPressed(settings.getPauseButton())) {
				paused = true;
				pauseDialog.show(interfaceStage);
				return;
			}

			// Stop if player is not controllable.
			if (!gameBoard.isControllable()) {
				return;
			}

			// Initiate queued move if there is one.
			if (queuedMove != null && !gameBoard.isOccupied()) {
				player.move(queuedMove.dx, queuedMove.dy);
				gameBoard.movePlayerModel(queuedMove.dx, queuedMove.dy);
				queuedMove = null;
				return;
			}

			// Queue player move if it is occupied; else move or restart.
			if (gameBoard.isOccupied()) {
				if (Gdx.input.isKeyJustPressed(settings.getForwardsButton())) {
					queuedMove = new Move(0, 1);
				} else if (Gdx.input.isKeyJustPressed(settings.getBackwardsButton())) {
					queuedMove = new Move(0, -1);
				} else if (Gdx.input.isKeyJustPressed(settings.getRightButton())) {
					queuedMove = new Move(1, 0);
				} else if (Gdx.input.isKeyJustPressed(settings.getLeftButton())) {
					queuedMove = new Move(-1, 0);
				}
			} else {
				if (Gdx.input.isKeyJustPressed(settings.getForwardsButton())) {
					player.move(0, 1);
					gameBoard.movePlayerModel(0, 1);
				} else if (Gdx.input.isKeyJustPressed(settings.getBackwardsButton())) {
					player.move(0, -1);
					gameBoard.movePlayerModel(0, -1);
				} else if (Gdx.input.isKeyJustPressed(settings.getRightButton())) {
					player.move(1, 0);
					gameBoard.movePlayerModel(1, 0);
				} else if (Gdx.input.isKeyJustPressed(settings.getLeftButton())) {
					player.move(-1, 0);
					gameBoard.movePlayerModel(-1, 0);
				} else if (Gdx.input.isKeyJustPressed(settings.getRestartButton())) {
					resetLevel();
				}
			}
		}

		// Show completion dialog if level is completed.
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
		if (!game.getSettings().getSoundMuted()) {
			game.getSound("Level-Solve").play(
					game.getSettings().getSoundVolume() / 100.0f);
		}
		completed = true;
		/*
		if (optimal)
			completeDialogNormal.show(interfaceStage);
		else
			completeDialogOptimal.show(interfaceStage);
		*/
		completeDialogNormal.show(interfaceStage);
		game.getSaveState().setSolved(level.getDifficulty(), level.getNumber());

		try {
			game.getSaveState().save();
		} catch (IOException e) {
			e.printStackTrace();
		}
		System.out.println(game.getSaveState().getSolveState(level.getDifficulty(), level.getNumber()));
	}

	public void setPaused(boolean paused) {
		this.paused = paused;
	}

	public void setPauseClosed(boolean pauseClosed) {
		this.pauseClosed = pauseClosed;
	}

}

