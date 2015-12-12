package com.game.mygame.desktop;

import com.badlogic.gdx.backends.lwjgl.LwjglApplication;
import com.badlogic.gdx.backends.lwjgl.LwjglApplicationConfiguration;
import com.badlogic.gdx.tools.texturepacker.TexturePacker;
import com.game.mygame.MyGame;

public class DesktopLauncher {

	public static void main (String[] arg) {
		LwjglApplicationConfiguration cfg = new LwjglApplicationConfiguration();
		cfg.title = "MyGame";
		cfg.width = 800;
		cfg.height = 600;
		cfg.samples = 4;

		cfg.vSyncEnabled = true;
		//cfg.foregroundFPS = 0;
		//cfg.backgroundFPS = 0;

		TexturePacker.process("/home/stefan/libGDX/MyGame/android/assets/anim",
				"/home/stefan/libGDX/MyGame/android/assets/anim", "anim");

		new LwjglApplication(new MyGame(), cfg);
	}

}
