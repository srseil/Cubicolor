package com.game.mygame;

import com.badlogic.gdx.graphics.g2d.Batch;
import com.badlogic.gdx.scenes.scene2d.ui.SelectBox;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;

public class CustomSelectBox<T> extends SelectBox<T> {

	public CustomSelectBox(Skin skin) {
		super(skin);
	}

	@Override
	public void draw(Batch batch, float parentAlpha) {
		getStyle().background.setBottomHeight(20.0f);
		super.draw(batch, parentAlpha);
	}

	@Override
	public float getHeight() {
		return (super.getHeight() - 14.0f);
	}

}

