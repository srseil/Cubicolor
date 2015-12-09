package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.Input;
import com.badlogic.gdx.Screen;
import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.Table;
import com.badlogic.gdx.utils.viewport.ExtendViewport;

public class GameScreen implements Screen {

	private MyGame game;
	private Level level;
	private Player player;
	private GameBoard gameBoard;

	private Stage stage;
	private Label steps;
	private PauseDialog pauseDialog;
	private WinDialog completeDialogNormal;
	private WinDialog completeDialogOptimal;

	private boolean paused;
	private boolean pauseClosed;
	private boolean completed;

	// FPS Counter
	private Label fps;

	public GameScreen(Level level, MyGame game) {
		this.level = level;
		this.game = game;

		player = new Player(level, this);
		gameBoard = new GameBoard(level, player, game);

		stage = new Stage(new ExtendViewport(800, 600));

		Table rootTable = new Table();
		rootTable.setFillParent(true);
		rootTable.setDebug(true);
		stage.addActor(rootTable);

		Skin skin = new Skin(Gdx.files.internal("uiskin.json"));

		Table leftUI = new Table();
		Table boardTable = new Table();
		Table rightUI = new Table();
		rootTable.add(leftUI).expandY().bottom().left();
		rootTable.add(boardTable).expand().center();
		rootTable.add(rightUI).bottom().right();

		Label message = new Label("Message", skin);
		leftUI.add(message);

		fps = new Label("FPS: ", skin);
		rightUI.add(fps);
		rightUI.row();
		steps = new Label("number of steps: ", skin);
		rightUI.add(steps);

		boardTable.add(gameBoard);

		pauseDialog = new PauseDialog(skin, this, game);
		completeDialogNormal = new WinDialog(false, skin, this, game);
		completeDialogOptimal = new WinDialog(true, skin, this, game);
	}

	// The render() method is being used as a hook into the game loop.
	@Override
	public void render(float delta) {
		// Set background color.
		Gdx.gl.glClearColor(0.85f, 0.8f, 0.7f, 1);
		Gdx.gl.glViewport(0, 0, Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT | GL20.GL_DEPTH_BUFFER_BIT);

		fps.setText("FPS: " + Gdx.graphics.getFramesPerSecond());

		steps.setText("number of steps: " + player.getSteps());

		// Process game logic and input.
		processInput();

		// Draw game board.
		stage.act(delta);
		stage.draw();
	}

	@Override
	public void show() {
		Gdx.input.setInputProcessor(stage);
	}

	@Override
	public void dispose() {
		stage.dispose();
	}

	@Override
	public void resize(int width, int height) {
		stage.getViewport().update(width, height, true);
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
			if (Gdx.input.isKeyJustPressed(Input.Keys.UP)) {
				player.move(0, 1);
				gameBoard.movePlayerModel(0, 1);
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.DOWN)) {
				player.move(0, -1);
				gameBoard.movePlayerModel(0, -1);
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.RIGHT)) {
				player.move(1, 0);
				gameBoard.movePlayerModel(1, 0);
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.LEFT)) {
				player.move(-1, 0);
				gameBoard.movePlayerModel(-1, 0);
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.R)) {
				resetLevel();
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.ESCAPE)) {
				paused = true;
				pauseDialog.show(stage);
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
			completeDialogNormal.show(stage);
		else
			completeDialogOptimal.show(stage);

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

