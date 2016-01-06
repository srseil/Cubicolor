package com.game.mygame;

import com.badlogic.gdx.Input;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.*;
import com.badlogic.gdx.scenes.scene2d.utils.ChangeListener;

public class SettingsMenu extends Table {

	private Stage stage;
	private Skin skin;
	private final Dialog resetDialog;

	public SettingsMenu(Skin skin, final Stage stage, final MyGame game) {
		this.stage = stage;
		this.skin = skin;

		// Confirmation dialog for resetting progress
		final Dialog confirmResetDialog = new Dialog("Progress reset", skin);
		confirmResetDialog.button("OK", null);
		confirmResetDialog.key(Input.Keys.ENTER, null);
		confirmResetDialog.key(Input.Keys.ESCAPE, null);

		// Dialog for "Reset progress" button
		resetDialog = new Dialog("Reset progress", skin) {
			@Override
			protected void result(Object object) {
				if ((Boolean) object) {
					game.getSaveState().reset();
					/*
					try {
						game.getSaveState().save();
					} catch (IOException e) {
						e.printStackTrace();
					}
					*/
					confirmResetDialog.show(stage);
				}
			}
		};
		resetDialog.text("This will reset all your level progress and lock " +
			"all the levels you have unlocked to far!");
		resetDialog.button("Reset", true);
		resetDialog.button("Cancel", false);
		resetDialog.key(Input.Keys.ESCAPE, false);

		createGameSection();
		createAudioSection();
		createVideoSection();
	}

	private void createGameSection() {
		// Section label
		add(new Label("Game", skin)).left();
		row();

		// Language drop down menu
		add(new Label("Language", skin)).left();
		row();
		SelectBox<String> languageDropdown = new SelectBox<String>(skin);
		languageDropdown.setItems("English", "Deutsch", "Espanol", "Russian");
		add(languageDropdown).left();

		// Reset progress button
		TextButton resetButton = new TextButton("Reset progress", skin);
		resetButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				resetDialog.show(stage);
			}
		});
		add(resetButton).left().padLeft(20.0f);
		row();
	}

	private void createAudioSection() {
		// Section label
		Label audioLabel = new Label("Audio", skin);
		add(audioLabel).left().padTop(20.0f);
		row();

		// Music and sound volume sliders
		add(new Label("Music Volume", skin)).left();
		add(new Label("Sound Volume", skin)).left().padLeft(20.0f);
		row();
		Slider musicSlider = new Slider(0.0f, 100.0f, 5.0f, false, skin);
		add(musicSlider);
		Slider soundSlider = new Slider(0.0f, 100.0f, 5.0f, false, skin);
		add(soundSlider).padLeft(20.0f);
		row();

		// Music and sound mute buttons and value labels
		Table musicTable = new Table();
		musicTable.add(new CheckBox("Mute", skin)).expandX().left();
		musicTable.add(new Label("100", skin)).right();
		Table soundTable = new Table();
		soundTable.add(new CheckBox("Mute", skin)).expandX().left().padLeft(20.0f);
		soundTable.add(new Label("100", skin)).right();
		add(musicTable).fillX();
		add(soundTable).fillX();
		row();

	}

	private void createVideoSection() {
		// Section label
		add(new Label("Video", skin)).left().padTop(20.0f);
		row();

		// Resolution drop down menu and fullscreen checkbox
		add(new Label("Resolution", skin)).left();
		row();
		SelectBox<String> resolutionDropdown = new SelectBox<String>(skin);
		resolutionDropdown.setItems("resolution 1", "resolution 2", "less text");
		add(resolutionDropdown).left();
		add(new CheckBox("Fullscreen", skin)).left().padLeft(20.0f);
		row();

		// Brightness and contrast sliders
		add(new Label("Brightness", skin)).left();
		add(new Label("Contrast", skin)).left().padLeft(20.0f);
		row();
		add(new Slider(0.0f, 100.0f, 5.0f, false, skin));
		add(new Slider(0.0f, 100.0f, 5.0f, false, skin)).padLeft(20.0f);
		row();

		// VSync and colorblind mode checkboxes
		add(new CheckBox("VSync", skin)).left();
		add(new CheckBox("Colorblind mode", skin)).left().padLeft(20.0f);
	}

}

