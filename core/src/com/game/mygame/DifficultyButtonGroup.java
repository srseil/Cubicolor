package com.game.mygame;

import com.badlogic.gdx.scenes.scene2d.ui.ButtonGroup;
import com.badlogic.gdx.scenes.scene2d.ui.TextButton;

public class DifficultyButtonGroup extends ButtonGroup<TextButton> {

	LevelMenu levelMenu;

	public DifficultyButtonGroup(LevelMenu levelMenu) {
		super();
		this.levelMenu = levelMenu;
	}

	@Override
	protected boolean canCheck(TextButton button, boolean newState) {
		boolean ret = super.canCheck(button, newState);

		if (getChecked() != null) {
			System.out.println(this.getChecked().getLabel());
			levelMenu.switchToOverview(this.getChecked().getLabel().getText().toString());
		}

		return ret;
	}

	public String getCheckedLabel() {
		if (getChecked() != null)
			return getChecked().getLabel().toString();
		else
			return "Normal";
	}

}
