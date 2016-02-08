package com.game.mygame;

import com.badlogic.gdx.scenes.scene2d.ui.ButtonGroup;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.TextButton;

public class DifficultyButtonGroup extends ButtonGroup<TextButton> {

	private LevelMenu levelMenu;
	private Skin skin;

	public DifficultyButtonGroup(LevelMenu levelMenu, Skin skin) {
		super();
		this.levelMenu = levelMenu;
		this.skin = skin;
	}

	@Override
	protected boolean canCheck(TextButton button, boolean newState) {
		boolean ret = super.canCheck(button, newState);

			/*
		if (getChecked() != null) {
			System.out.println(this.getChecked().getLabel());

			button.setStyle(
					skin.get("toggle", TextButton.TextButtonStyle.class));

			Difficulty difficulty;
			switch (this.getChecked().getLabel().getText().toString()) {
				case "Normal": difficulty = Difficulty.NORMAL; break;
				case "Smart": difficulty = Difficulty.SMART; break;
				case "Genius": difficulty = Difficulty.GENIUS; break;
				default: difficulty = Difficulty.NORMAL;
			}
			levelMenu.switchToOverview(difficulty);
		} else {
			System.out.println("old changed");
			button.setStyle(
					skin.get("default", TextButton.TextButtonStyle.class));
		}
			*/

		return ret;
	}

	public String getCheckedLabel() {
		//if (getChecked() != null)
			return getChecked().getLabel().toString();
		//else
		//	return "Normal";
	}

}

