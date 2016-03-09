package com.game.mygame;

import com.badlogic.gdx.audio.Sound;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.g2d.NinePatch;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.Touchable;
import com.badlogic.gdx.scenes.scene2d.ui.*;
import com.badlogic.gdx.scenes.scene2d.utils.ChangeListener;
import com.badlogic.gdx.scenes.scene2d.utils.NinePatchDrawable;

import java.util.EnumMap;

public class LevelMenu extends Table {

	private MyGame game;
	private Table levelOverview;
	private EnumMap<Difficulty, Table> levelOverviews;
	private EnumMap<Difficulty, TextButton[]> levelButtons;
	private ButtonGroup<TextButton> difficultyButtons;
	private TextButton.TextButtonStyle unlockedStyle, completedStyle, lockedStyle, selectStyle;
	private NinePatchDrawable lockedBackground;
	private Sound buttonSound;

	public LevelMenu(Skin skin, MyGame game) {
		super(skin);
		this.game = game;
		this.setBackground("menu-background");
		buttonSound = game.getSound("Button-Click");

		// Level button styles
		selectStyle = game.getSkin().get(
				"select", TextButton.TextButtonStyle.class);
		unlockedStyle = game.getSkin().get(
				"default", TextButton.TextButtonStyle.class);
		NinePatch unlockedPatch = game.getSkin().get(
				"default-round", NinePatch.class);
		NinePatchDrawable unlockedBackground  =
				new NinePatchDrawable(unlockedPatch);
		unlockedStyle.up = unlockedBackground;
		unlockedStyle.checked = unlockedBackground;
		completedStyle = new TextButton.TextButtonStyle(unlockedStyle);
		NinePatch completedPatch = game.getSkin().get(
				"default-round-completed", NinePatch.class);
		NinePatchDrawable completedBackground  =
				new NinePatchDrawable(completedPatch);
		completedStyle.up = completedBackground;
		completedStyle.checked = completedBackground;
		NinePatch completedDownPatch = game.getSkin().get(
				"default-round-down-completed", NinePatch.class);
		completedStyle.down = new NinePatchDrawable(completedDownPatch);

		lockedStyle = new TextButton.TextButtonStyle(unlockedStyle);
		NinePatch backgroundPatch =
				game.getSkin().get("level-locked", NinePatch.class);
		lockedBackground = new NinePatchDrawable(backgroundPatch);
		lockedStyle.up = lockedBackground;
		lockedStyle.checked = lockedBackground;
		lockedStyle.fontColor = Color.CLEAR;

		// Difficulty buttons
		TextButton normalButton = new TextButton("Normal", selectStyle);
		TextButton smartButton = new TextButton("Smart", selectStyle);
		TextButton geniusButton = new TextButton("Genius", selectStyle);

		// Difficulty button group on top of level buttons
		HorizontalGroup buttons = new HorizontalGroup();
		buttons.space(5.0f);
		buttons.addActor(normalButton);
		buttons.addActor(smartButton);
		buttons.addActor(geniusButton);
		this.add(buttons);
		this.row();

		// Level buttons
		levelOverviews = new EnumMap<>(Difficulty.class);
		levelButtons = new EnumMap<>(Difficulty.class);
		for (Difficulty difficulty : Difficulty.values()) {
			levelOverviews.put(difficulty, new Table());
			levelButtons.put(difficulty, new TextButton[16]);
			for (int i = 0; i < 16; i++) {
				if (i % 4 == 0)
					levelOverviews.get(difficulty).row();
				TextButton button = new TextButton(String.valueOf(i + 1), skin);
				levelButtons.get(difficulty)[i] = button;
				button.addListener(createLevelListener(i + 1));
				Cell cell = levelOverviews.get(difficulty)
						.add(button).width(50.0f).height(50.0f);
				if (i % 4 != 0)
					cell.padLeft(10.0f);
				if (i >= 4)
					cell.padTop(10.0f);
				if (i >= 12)
					cell.padBottom(10.0f);
			}
		}
		levelOverview = levelOverviews.get(Difficulty.NORMAL);
		this.add(levelOverview).padTop(15.0f);

		// Level difficulty buttons
		difficultyButtons = new ButtonGroup<>();
		difficultyButtons.add(normalButton);
		difficultyButtons.add(smartButton);
		difficultyButtons.add(geniusButton);
		difficultyButtons.setMinCheckCount(1);
		difficultyButtons.setMaxCheckCount(1);
		difficultyButtons.setUncheckLast(true);
		difficultyButtons.setChecked("Normal");

		normalButton.addListener(createDifficultyListener(Difficulty.NORMAL));
		smartButton.addListener(createDifficultyListener(Difficulty.SMART));
		geniusButton.addListener(createDifficultyListener(Difficulty.GENIUS));

		updateLevelButtons();
	}

	private ChangeListener createLevelListener(final int number) {
		return new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				if (!game.getSettings().getSoundMuted())
					buttonSound.play(game.getSettings().getSoundVolume()/100f);
				game.openLevel(Difficulty.valueOf(difficultyButtons.getChecked()
						.getLabel().getText().toString().toUpperCase()), number);
			}
		};
	}

	private ChangeListener createDifficultyListener(final Difficulty diff) {
		return new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				TextButton button = (TextButton) actor;
				if (button.isChecked() && !game.getSettings().getSoundMuted())
					buttonSound.play(game.getSettings().getSoundVolume()/100f);

				Table overview = levelOverviews.get(diff);
				getCell(levelOverview).setActor(overview);
				levelOverview = overview;
			}
		};
	}

	/*
	 * Deactivate buttons for locked levels and activate unlocked ones.
	 */
	public void updateLevelButtons() {
		for (Difficulty difficulty : Difficulty.values()) {
			for (int i = 0; i < 16; i++) {
				TextButton button = levelButtons.get(difficulty)[i];
				if (game.getSaveState().isUnlocked(difficulty, i+2)) {
					button.setTouchable(Touchable.enabled);
					button.setStyle(completedStyle);
				} else if (game.getSaveState().isUnlocked(difficulty, i+1)) {
					button.setTouchable(Touchable.enabled);
					button.setStyle(unlockedStyle);
				} else {
					button.setTouchable(Touchable.disabled);
					button.setStyle(lockedStyle);
				}
			}
		}
	}

}

