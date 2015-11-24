package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.Input;
import com.badlogic.gdx.Screen;
import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.graphics.OrthographicCamera;
import com.badlogic.gdx.graphics.glutils.ShapeRenderer;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.*;
import com.badlogic.gdx.scenes.scene2d.utils.ChangeListener;
import com.badlogic.gdx.utils.viewport.ExtendViewport;

public class MenuScreen implements Screen {

	// DISPOSE! -> Documentation

	final MyGame game;

	private OrthographicCamera camera;
	private Stage stage;
	private Table rootTable;
	private Skin skin;

	public MenuScreen(OrthographicCamera camera, MyGame game) {
		this.game = game;

		//camera = new OrthographicCamera();
		this.camera = camera;
		//camera.setToOrtho(false);
		stage = new Stage(new ExtendViewport(800, 600, camera));
		//Gdx.input.setInputProcessor(stage);

		rootTable = new Table();
		rootTable.setFillParent(true);
		rootTable.setDebug(true);
		rootTable.left();
		stage.addActor(rootTable);

		skin = new Skin(Gdx.files.internal("uiskin.json"));

		//Table playContent = new Table();
		LevelMenu levelMenu = new LevelMenu(skin, game);
		Table settingsContent = new Table();
		//Table menuItems = new Table();
		VerticalGroup menuItems = new VerticalGroup();
		//menuItems.setWidth(100.0f);
		menuItems.fill();
		Table menuContent = new Table();
		rootTable.add(menuItems).expandY().padLeft(50.0f);
		//rootTable.add(menuContent).expandY().padLeft(50.0f);

		Label label = new Label("This is a list of menu items.", skin);
		menuItems.addActor(label);

		TextButton playButton = new TextButton("Play", skin);
		playButton.setWidth(100.0f);
		playButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				rootTable.removeActor(settingsContent);
				rootTable.add(levelMenu);
			}
		});

		TextButton settingsButton = new TextButton("Settings", skin);
		settingsButton.setWidth(100.0f);
		settingsButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				rootTable.removeActor(levelMenu);
				rootTable.add(settingsContent);
			}
		});

		TextButton quitButton = new TextButton("Quit", skin);
		quitButton.setWidth(100.0f);

		menuItems.addActor(playButton);
		menuItems.addActor(settingsButton);
		menuItems.addActor(quitButton);

		// Quit dialog.
		// !!! Exit <-> Quit
		Dialog quitDialog = new Dialog("Quit", skin) {
			protected void result(Object object) {
				if ((Boolean) object)
					Gdx.app.exit();
			}
		};
		quitDialog.text("Are you sure you want to quit?");
		quitDialog.button("Quit", true);
		quitDialog.button("Cancel", false);
		quitDialog.key(Input.Keys.ENTER, true);
		quitDialog.key(Input.Keys.ESCAPE, false);

		// Quit button action.
		quitButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				quitDialog.show(stage);
			}
		});


		// Play content table (level screen).
		TextButton easyButton = new TextButton("Easy", skin);
		easyButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				game.setScreen(game.getGameScreen());
				System.out.println("To Game Screen...");
			}
		});
		TextButton normalButton = new TextButton("Normal", skin);
		TextButton hardButton = new TextButton("Hard", skin);
		// Add to screen:
		//rootTable.add(playContent).left().padLeft(50.0f);

		// Settings content table.
		/* Resolution, full/windowed screen, language, (reset progress), brightness, (vsync),
			Music, SFX, Controls!, (colorblind? -> reddit), Tutorial
		 */

		settingsContent.add(new Label("Game", skin)).left();
		settingsContent.row();
		settingsContent.add(new Label("Language", skin)).left();
		settingsContent.row();
		SelectBox<String> languageDropdown = new SelectBox<>(skin);
		languageDropdown.setItems("English", "Deutsch", "Espanol", "Russian");
		settingsContent.add(languageDropdown).left();
		TextButton resetButton = new TextButton("Reset progress", skin);
		settingsContent.add(resetButton).left().padLeft(20.0f);
		settingsContent.row();

		Label audioLabel = new Label("Audio", skin);
		settingsContent.add(audioLabel).left().padTop(20.0f);
		settingsContent.row();

		settingsContent.add(new Label("Music Volume", skin)).left();
		settingsContent.add(new Label("Sound Volume", skin)).left().padLeft(20.0f);
		settingsContent.row();

		Slider musicSlider = new Slider(0.0f, 100.0f, 5.0f, false, skin);
		settingsContent.add(musicSlider);
		Slider soundSlider = new Slider(0.0f, 100.0f, 5.0f, false, skin);
		settingsContent.add(soundSlider).padLeft(20.0f);
		settingsContent.row();

		Table musicTable = new Table();
		musicTable.debug();
		musicTable.add(new CheckBox("Mute", skin)).expandX().left();
		musicTable.add(new Label("100", skin)).right();
		Table soundTable = new Table();
		soundTable.debug();
		soundTable.add(new CheckBox("Mute", skin)).expandX().left().padLeft(20.0f);
		soundTable.add(new Label("100", skin)).right();
		settingsContent.add(musicTable).fillX();
		settingsContent.add(soundTable).fillX();
		settingsContent.row();

		settingsContent.add(new Label("Video", skin)).left().padTop(20.0f);
		settingsContent.row();
		settingsContent.add(new Label("Resolution", skin)).left();
		settingsContent.row();
		SelectBox<String> resolutionDropdown = new SelectBox<>(skin);
		resolutionDropdown.setItems("resolution 1", "resolution 2", "less text");
		settingsContent.add(resolutionDropdown).left();
		settingsContent.add(new CheckBox("Fullscreen", skin)).left().padLeft(20.0f);
		settingsContent.row();

		settingsContent.add(new Label("Brightness", skin)).left();
		settingsContent.add(new Label("Contrast", skin)).left().padLeft(20.0f);
		settingsContent.row();
		settingsContent.add(new Slider(0.0f, 100.0f, 5.0f, false, skin));
		settingsContent.add(new Slider(0.0f, 100.0f, 5.0f, false, skin)).padLeft(20.0f);
		settingsContent.row();

		settingsContent.add(new CheckBox("VSync", skin)).left();
		settingsContent.add(new CheckBox("Colorblind mode", skin)).left().padLeft(20.0f);


		rootTable.add(settingsContent).left().padLeft(50.0f);

		//Gdx.graphics.setDisplayMode(800, 600, true);
		//Gdx.graphics.setVSync(true);

		//rootTable.add(new LevelMenu(skin, game)).left().padLeft(50.0f);
	}


	@Override
	public void show() {
		stage.getViewport().setCamera(camera);
		camera.update();
		Gdx.input.setInputProcessor(stage);
	}

	@Override
	public void render(float delta) {
		camera.update();
		game.getShapeRenderer().setProjectionMatrix(camera.combined);
		game.getSpriteBatch().setProjectionMatrix(camera.combined);

		// Set background color.
		Gdx.gl.glClearColor(0.85f, 0.8f, 0.7f, 1);
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT);

		game.getShapeRenderer().begin(ShapeRenderer.ShapeType.Filled);
		stage.act(delta);
		stage.draw();

		game.getShapeRenderer().end();
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
		skin.dispose();
	}
}
