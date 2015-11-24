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

		levelOverview = new Table();
		levelButtons = new TextButton[16];
		for (int i = 0; i < 16; i++) {
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
		difficultyButtons.setChecked("Normal");
		difficultyButtons.setMinCheckCount(1);
		difficultyButtons.setMaxCheckCount(1);
		difficultyButtons.setUncheckLast(true);
	}

	private ChangeListener createChangeListener(int n) {
		return new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				System.out.println(n);
				game.openLevel(difficultyButtons.getCheckedLabel(), n);
			}
		};
	}

	public void switchToOverview(String difficulty) {
		String identifier = difficulty.substring(0, 1);

		for (int i = 0; i < 16; i++) {
			levelButtons[i].setText(Integer.toString(i+1) + identifier);
		}
	}

}
