package com.game.mygame;

import com.badlogic.gdx.graphics.g2d.NinePatch;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.ui.*;
import com.badlogic.gdx.scenes.scene2d.utils.ChangeListener;
import com.badlogic.gdx.scenes.scene2d.utils.NinePatchDrawable;

import java.util.EnumMap;

public class LevelMenu extends Table {

	private MyGame game;
	private Table levelOverview;
	private EnumMap<Difficulty, Table> levelOverviews;
	private TextButton[][] levelButtons;
	private DifficultyButtonGroup difficultyButtons;

	public LevelMenu(Skin skin, MyGame game) {
		super(skin);
		this.game = game;
		this.setBackground("menu-background");

		// Difficulty buttons
		TextButton normalButton = new TextButton("Normal", skin);
		TextButton smartButton = new TextButton("Smart", skin);
		TextButton geniusButton = new TextButton("Genius", skin);

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
		for (Difficulty difficulty : Difficulty.values()) {
			levelOverviews.put(difficulty, new Table());
			for (int i = 0; i < 16; i++) {
				if (i % 4 == 0)
					levelOverviews.get(difficulty).row();
				TextButton button = new TextButton(String.valueOf(i + 1), skin);
				button.addListener(createChangeListener(i + 1));
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
		difficultyButtons = new DifficultyButtonGroup(this, skin);
		difficultyButtons.add(normalButton);
		difficultyButtons.add(smartButton);
		difficultyButtons.add(geniusButton);
		difficultyButtons.setMinCheckCount(1);
		difficultyButtons.setMaxCheckCount(1);
		difficultyButtons.setUncheckLast(true);
		difficultyButtons.setChecked("Normal");

		updateLevelButtons();
	}

	private ChangeListener createChangeListener(final int n) {
		return new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				System.out.println("Starting: " + n);
				game.openLevel(difficultyButtons.getCheckedLabel(), n);
				// Switch again to update buttons:
				//switchToOverview();
			}
		};
	}

	public void switchToOverview(Difficulty difficulty) {
		/*
		levelButtons[0].setText(Integer.toString(1));

		// Set lock of first smart and genius level.
		if (difficulty == Difficulty.SMART) {
			if (game.getSaveState().getSolveState(Difficulty.NORMAL,
					levelButtons.length) == SolveState.UNSOLVED)
				levelButtons[0].setDisabled(true);
			else
				levelButtons[0].setDisabled(false);
			System.out.println(levelButtons[0].isDisabled());
		} else if (difficulty == Difficulty.GENIUS) {
			if (game.getSaveState().getSolveState(Difficulty.SMART,
					levelButtons.length) == SolveState.UNSOLVED)
				levelButtons[0].setDisabled(true);
			else
				levelButtons[0].setDisabled(false);
		} else {
			levelButtons[0].setDisabled(false);
			System.out.println("normal false");
		}


		// Set lock for all other levels.
		for (int i = 1; i < levelButtons.length; i++) {
			levelButtons[i].setText(Integer.toString(i+1) + difficulty);
			// Check if previous level (i instead of i+1) is solved.
			if (game.getSaveState().getSolveState(difficulty, i)
					== SolveState.UNSOLVED) {
				levelButtons[i].setDisabled(true);
			} else {
				levelButtons[i].setDisabled(false);
			}
		}
		*/
	}

	public void updateLevelButtons() {
	}

}

