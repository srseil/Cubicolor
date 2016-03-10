package com.game.mygame;

import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.g2d.Sprite;
import com.badlogic.gdx.scenes.scene2d.ui.Image;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.Table;
import com.badlogic.gdx.scenes.scene2d.utils.Drawable;
import com.badlogic.gdx.utils.Align;
import com.badlogic.gdx.utils.Scaling;

public class CreditsMenu extends Table {

	public CreditsMenu(Skin skin, MyGame game) {
		super(skin);
		setBackground("menu-background");

		Image logo = new Image(skin.getDrawable("logo"), Scaling.none);
		add(logo).colspan(2).align(Align.center);
		row();

		Label l = new Label("A game by Stefan Seil", skin);
		l.setColor(Color.BLACK);
		add(l).colspan(2).expandX().align(Align.center).padTop(5f);
		row();

		Label.LabelStyle s = new Label.LabelStyle(
				game.getBitmapFont("Vollkorn-Regular-16"), Color.BLACK);
		l = new Label("Relaxing Piano Music by Kevin MacLeod (incompetech.com)\n" +
				"Licensed under Creative Commons: By Attribution 3.0 License\n" +
				"https://creativecommons.org/licenses/by/3.0/", skin);
		l.setStyle(s);
		l.setAlignment(Align.center);
		add(l).colspan(2).padTop(10f).padBottom(10f);
		row();

		l = new Label("@Moltenplay", skin);
		l.setColor(Color.BLACK);
		add(l).left();
		l = new Label("Moltenplay.com", skin);
		l.setColor(Color.BLACK);
		add(l).right().padLeft(30.0f);

		//debug();
	}

}

