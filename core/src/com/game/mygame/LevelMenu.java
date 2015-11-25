package com.game.mygame;

import com.badlogic.gdx.Screen;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.Table;
import com.badlogic.gdx.scenes.scene2d.ui.TextButton;
import com.badlogic.gdx.scenes.scene2d.utils.ChangeListener;

public class LevelMenu extends Table {

	private MyGame game;
	private Table levelOverview;
	private TextButton[] levelButtons;
	private DifficultyButtonGroup difficultyButtons;

	public LevelMenu(Skin skin, MyGame game) {
		super();

		this.game = game;

		TextButton normalButton = new TextButton("Normal", skin);
		TextButton smartButton = new TextButton("Smart", skin);
		TextButton geniusButton = new TextButton("Genius", skin);
		this.add(normalButton);
		this.add(smartButton);
		this.add(geniusButton);
		this.row();

		levelButtons = new TextButton[3];
		levelOverview = new Table();
		for (int i = 0; i < levelButtons.length; i++) {
			if (i % 4 == 0)
				levelOverview.row();
			levelButtons[i] = new TextButton("", skin);
			levelButtons[i].addListener(createChangeListener(i+1));
			levelOverview.add(levelButtons[i]);
		}
		this.add(levelOverview);

		difficultyButtons = new DifficultyButtonGroup(this);
		difficultyButtons.add(normalButton);
		difficultyButtons.add(smartButton);
		difficultyButtons.add(geniusButton);
		difficultyButtons.setMinCheckCount(1);
		difficultyButtons.setMaxCheckCount(1);
		difficultyButtons.setUncheckLast(true);
		difficultyButtons.setChecked("Normal");
	}

	private ChangeListener createChangeListener(int n) {
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
		//String identifier = difficulty.substring(0, 1);
		levelButtons[0].setText(Integer.toString(1) + difficulty);

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
	}

}
