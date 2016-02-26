package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.Input;
import com.badlogic.gdx.Screen;
import com.badlogic.gdx.audio.Sound;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.graphics.OrthographicCamera;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.InputEvent;
import com.badlogic.gdx.scenes.scene2d.InputListener;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.*;
import com.badlogic.gdx.scenes.scene2d.utils.ChangeListener;
import com.badlogic.gdx.utils.Align;
import com.badlogic.gdx.utils.viewport.ExtendViewport;

public class MenuScreen implements Screen {

	// DISPOSE! -> Documentation

	private final MyGame game;
	private Stage stage;
	private OrthographicCamera camera;
	private Skin skin;
	private Table rootTable;
	private Table currentMenu;
	private final LevelMenu levelMenu;
	private final ControlsMenu controlsMenu;
	private Sound buttonSound;

	public MenuScreen(final MyGame game) {
		this.game = game;
		stage = new Stage(new ExtendViewport(
				Gdx.graphics.getWidth(), Gdx.graphics.getHeight()));
		camera = (OrthographicCamera) stage.getCamera();
		skin = game.getSkin();
		buttonSound = game.getSound("Button-Click");

		TextButton.TextButtonStyle buttonStyle = skin.get(
				"select", TextButton.TextButtonStyle.class);

		rootTable = new Table();
		rootTable.setFillParent(true);
		//rootTable.setDebug(true);
		stage.addActor(rootTable);

		final Table contentTable = new Table();

		levelMenu = new LevelMenu(skin, game);
		controlsMenu = new ControlsMenu(skin, stage, game);
		final SettingsMenu settingsMenu = new SettingsMenu(skin, stage, game);
		final CreditsMenu creditsMenu = new CreditsMenu(skin, game);

		// Quit dialog
		final Dialog quitDialog = new Dialog("Quit", skin) {
			protected void result(Object object) {
				if ((Boolean) object)
					Gdx.app.exit();
			}
		};
		quitDialog.getTitleLabel().setAlignment(Align.center);
		Label quitLabel = new Label("Are you sure you want to quit?", skin);
		quitLabel.setColor(Color.BLACK);
		quitDialog.text(quitLabel);
		TextButton quitQuit = new TextButton("Quit", skin);
		quitQuit.addListener(game.createClickListener());
		quitDialog.button(quitQuit, true);
		quitDialog.key(Input.Keys.ENTER, true);
		TextButton quitCancel = new TextButton("Cancel", skin);
		quitCancel.addListener(game.createClickListener());
		quitDialog.button(quitCancel, false);
		quitDialog.key(Input.Keys.ESCAPE, false);

		// Group of menu buttons
		VerticalGroup menuItems = new VerticalGroup();
		menuItems.fill();
		menuItems.space(5.0f);

		// Play button
		final TextButton playButton = new TextButton("Play", skin);
		playButton.setStyle(buttonStyle);
		playButton.getLabelCell().width(120.0f);
		playButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				if (currentMenu != levelMenu) {
					System.out.println(game.getSettings().getSoundVolume());
					if (!game.getSettings().getSoundMuted())
						buttonSound.play(game.getSettings().getSoundVolume()/100f);
					levelMenu.updateLevelButtons();
					contentTable.getCell(currentMenu).setActor(levelMenu);
					currentMenu = levelMenu;
				}
			}
		});
		menuItems.addActor(playButton);

		// Controls button
		final TextButton controlsButton = new TextButton("Controls", skin);
		controlsButton.setStyle(buttonStyle);
		controlsButton.setWidth(120.0f);
		controlsButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				if (currentMenu != controlsMenu) {
					if (!game.getSettings().getSoundMuted())
						buttonSound.play(game.getSettings().getSoundVolume() / 100f);
					contentTable.getCell(currentMenu).setActor(controlsMenu);
					currentMenu = controlsMenu;
				}
			}
		});
		menuItems.addActor(controlsButton);

		// Settings button
		final TextButton settingsButton = new TextButton("Settings", skin);
		settingsButton.setStyle(buttonStyle);
		settingsButton.setWidth(120.0f);
		settingsButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				if (currentMenu != settingsMenu) {
					if (!game.getSettings().getSoundMuted())
						buttonSound.play(game.getSettings().getSoundVolume()/100f);
					contentTable.getCell(currentMenu).setActor(settingsMenu);
					currentMenu = settingsMenu;
				}
			}
		});
		menuItems.addActor(settingsButton);

		// Credits button
		final TextButton creditsButton = new TextButton("Credits", skin);
		creditsButton.setStyle(buttonStyle);
		creditsButton.setWidth(120.0f);
		creditsButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				if (currentMenu != creditsMenu) {
					if (!game.getSettings().getSoundMuted())
						buttonSound.play(game.getSettings().getSoundVolume()/100f);
					contentTable.getCell(currentMenu).setActor(creditsMenu);
					currentMenu = creditsMenu;
				}
			}
		});
		menuItems.addActor(creditsButton);

		// Quit button
		TextButton quitButton = new TextButton("Quit", skin);
		quitButton.setWidth(120.0f);
		quitButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				if (!game.getSettings().getSoundMuted())
					buttonSound.play(game.getSettings().getSoundVolume()/100f);
				quitDialog.show(stage);
			}
		});
		menuItems.addActor(quitButton);

		// Version label
		Label version = new Label("Cubicolor " + MyGame.GAME_VERSION, skin);
		version.setColor(Color.BLACK);
		version.setStyle(new Label.LabelStyle(
				game.getBitmapFont("Vollkorn-Regular-24"), Color.BLACK));

		contentTable.left();
		contentTable.add(levelMenu).left();
		rootTable.add(menuItems).expand().right().padRight(50.0f);//.padLeft(50.0f);
		rootTable.add(contentTable).size(0.45f*Gdx.graphics.getWidth(),
				Gdx.graphics.getHeight()).left().padRight(50.0f);
		rootTable.add(version).expandY().right().bottom().padBottom(2.0f).padRight(5.0f);
		currentMenu = levelMenu;
		//stage.addActor(currentMenu);

		ButtonGroup<TextButton> menuButtons = new ButtonGroup<>();
		menuButtons.add(playButton);
		menuButtons.add(controlsButton);
		menuButtons.add(settingsButton);
		menuButtons.add(creditsButton);
		menuButtons.setMaxCheckCount(1);
		menuButtons.setMinCheckCount(1);
		menuButtons.setUncheckLast(true);
		menuButtons.setChecked("Play");
	}


	@Override
	public void show() {
		camera.setToOrtho(false, 800, 600);
		//stage.getViewport().setCamera(camera);
		//camera.update();
		Gdx.input.setInputProcessor(stage);
	}

	@Override
	public void render(float delta) {
		//camera.update();
		//Gdx.gl.glClearColor(0.85f, 0.8f, 0.7f, 1);
		Gdx.gl.glClearColor(0.949f, 0.941f, 0.925f, 1.0f);
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT);

		// Update and draw menu content.
		stage.act(delta);
		stage.draw();
	}

	@Override
	public void resize(int width, int height) {
		stage.getViewport().update(width, height, true);
	}

	@Override
	public void pause() {

	}

	@Override
	public void resume() {

	}

	@Override
	public void hide() {

	}

	@Override
	public void dispose() {
		stage.dispose();
	}

	public LevelMenu getLevelMenu() {
		return levelMenu;
	}

}

