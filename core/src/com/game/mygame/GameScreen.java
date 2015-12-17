package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.Input;
import com.badlogic.gdx.Screen;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.scenes.scene2d.Group;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.Stack;
import com.badlogic.gdx.scenes.scene2d.ui.Table;
import com.badlogic.gdx.utils.viewport.ExtendViewport;

public class GameScreen implements Screen {

	// Remove camera from GameBoard and use the one from boardStage (then just initialize with proper values)

	private MyGame game;
	private Level level;
	private Player player;
	private GameBoard gameBoard;

	private Stage boardStage;
	private Stage interfaceStage;

	private Stage stage;
	private PauseDialog pauseDialog;
	private WinDialog completeDialogNormal;
	private WinDialog completeDialogOptimal;
	private Label levelLabel;
	private Label difficultyLabel;
	private Label stepsLabel;
	private String stepsText;
	private int steps;

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

		boardStage = new Stage(new ExtendViewport(800, 600));
		boardStage.addActor(gameBoard);
		interfaceStage = new Stage(new ExtendViewport(800, 600));

		/*
		Stack stack = new Stack();
		stack.add(gameBoard);

		Group rootGroup = new Group();
		rootGroup.addActor(gameBoard);
		//stage.addActor(gameBoard);
		*/

		Table rootTable = new Table();
		rootTable.setFillParent(true);
		//rootTable.setDebug(true);
		//stage.addActor(rootTable);
		//rootGroup.addActor(rootTable);
		//stack.add(rootTable);
		//stage.addActor(stack);
		interfaceStage.addActor(rootTable);

		Skin skin = new Skin(Gdx.files.internal("uiskin.json"));


		//Group bg = new Group();
		//Group fg = new Group();
		//Table bg = new Table();
		//bg.setFillParent(true);
		//Table fg = new Table();
		//fg.setFillParent(true);
		//rootTable.add(bg);
		//rootTable.add(fg);
		//stage.addActor(bg);
		//stage.addActor(fg);



		Table leftUI = new Table();
		//leftUI.debug();
		//Table boardTable = new Table();
		//boardTable.add(gameBoard);
		Table rightUI = new Table();
		//rightUI.debug();
		//rightUI.debug();
		//rootTable.add(boardTable).expand().center();
		//rootTable.add(gameBoard).fill().expand().center();
		rootTable.add(leftUI).fillY().expand().left().padLeft(10.0f).padTop(10.0f);
		rootTable.add(rightUI).fillY().right().padRight(10.0f).padBottom(10.0f).width(65.0f);


		//bg.add(gameBoard);
		//fg.add(leftUI).left().fillY();
		//fg.add(rightUI).right().fillY();

		//rootTable.add(bg).center();
		//rootTable.add(fg).fill();

		System.out.println("TEST: " + level.getNumber());
		difficultyLabel = new Label(level.getDifficulty().toString(), skin);
		difficultyLabel.setStyle(new Label.LabelStyle(game.getBitmapFont("OldStandard-Regular-60"), Color.RED));
		leftUI.add(difficultyLabel).top().padTop(-10.0f);
		leftUI.row();
		levelLabel = new Label("Level " + level.getNumber(), skin);
		levelLabel.setStyle(new Label.LabelStyle(game.getBitmapFont("OldStandard-Regular-30"), Color.BLACK));
		leftUI.add(levelLabel).expandY().top().padTop(-5.0f);
		//Label message = new Label("Message", skin);
		//leftUI.add(message);

		fps = new Label("FPS: ", skin);
		rightUI.add(fps).expandY().top();
		rightUI.row();
		stepsLabel = new Label("00", skin);
		stepsLabel.setStyle(new Label.LabelStyle(game.getBitmapFont("OldStandard-Regular-60"), Color.RED));
		rightUI.add(stepsLabel).bottom().padBottom(-18.0f);
		rightUI.row();
		Label stepsCaption = new Label("steps", skin);
		stepsCaption.setStyle(new Label.LabelStyle(game.getBitmapFont("OldStandard-Regular-30"), Color.BLACK));
		rightUI.add(stepsCaption).bottom();


		steps = player.getSteps();

		pauseDialog = new PauseDialog(skin, this, game);
		completeDialogNormal = new WinDialog(false, skin, this, game);
		completeDialogOptimal = new WinDialog(true, skin, this, game);
	}

	// The render() method is being used as a hook into the game loop.
	@Override
	public void render(float delta) {
		// Set background color.
		//Gdx.gl.glClearColor(0.85f, 0.8f, 0.7f, 1);
		Gdx.gl.glClearColor(0.949f, 0.941f, 0.925f, 1.0f);

		//Gdx.gl.glClearColor();

		//Gdx.gl.glClearColor(1.0f, 1.0f, 1.0f, 1.0f);
		Gdx.gl.glViewport(0, 0, Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT | GL20.GL_DEPTH_BUFFER_BIT);

		fps.setText("FPS: " + Gdx.graphics.getFramesPerSecond());

		if (player.getSteps() != steps) {
			steps = player.getSteps();
			stepsText = Integer.toString(steps);
			if (stepsText.length() == 1)
				stepsLabel.setText("0" + stepsText);
			else
				stepsLabel.setText(stepsText);
		}

		// Process game logic and input.
		processInput();

		// Draw game board.
		//stage.act(delta);
		//stage.draw();
		boardStage.act();
		boardStage.draw();
		interfaceStage.act();
		interfaceStage.draw();
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

