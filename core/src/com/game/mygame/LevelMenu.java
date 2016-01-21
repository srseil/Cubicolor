package com.game.mygame;

import com.badlogic.gdx.graphics.g2d.NinePatch;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.Table;
import com.badlogic.gdx.scenes.scene2d.ui.TextButton;
import com.badlogic.gdx.scenes.scene2d.utils.ChangeListener;
import com.badlogic.gdx.scenes.scene2d.utils.NinePatchDrawable;

import java.util.EnumMap;

public class LevelMenu extends Table {

	private MyGame game;
	private Table levelOverview;
	private EnumMap<Difficulty, Table> levelOverviews;
	private TextButton[][] levelButtons;
	private DifficultyButtonGroup difficultyButtons;

	/*
	Bug: in switchToOverview(), wenn setText() aufgerufen wird, wird das
	Layout der gesamten Hierarchie zerschossen. (SettingsMenu wird in neue
	Zeile verschoben).
	 */

	public LevelMenu(Skin skin, MyGame game) {
		super(skin);

		this.game = game;

		// Set background texture.
		NinePatch backgroundPatch =
				game.getSkin().get("menu-background", NinePatch.class);
		NinePatchDrawable background = new NinePatchDrawable(backgroundPatch);
		this.setBackground(background);

		TextButton normalButton = new TextButton("Normal", skin);
		TextButton smartButton = new TextButton("Smart", skin);
		TextButton geniusButton = new TextButton("Genius", skin);
		this.add(normalButton);
		this.add(smartButton);
		this.add(geniusButton);
		this.row();

		//levelOverview = new Table();
		levelOverviews = new EnumMap<>(Difficulty.class);

		// Level buttons
		//levelButtons = new TextButton[Difficulty.values().length][4];
		//for (int i = 0; i < levelButtons.length; i++) {
		for (Difficulty difficulty : Difficulty.values()) {
			levelOverviews.put(difficulty, new Table());
			for (int i = 0; i < 4; i++) {
				if (i % 4 == 0)
					levelOverviews.get(difficulty).row();
				TextButton button = new TextButton(String.valueOf(i + 1), skin);
				button.getLabelCell().width(36.0f);
				button.addListener(createChangeListener(i + 1));
				levelOverviews.get(difficulty).add(button).padLeft(10.0f);
				System.out.println(button.getLabelCell().getMinHeight());
			}
		}
		levelOverview = levelOverviews.get(Difficulty.NORMAL);
		this.add(levelOverview).padTop(15.0f);

		// Level difficulty buttons
		difficultyButtons = new DifficultyButtonGroup(this);
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
				System.out.println(n);
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

