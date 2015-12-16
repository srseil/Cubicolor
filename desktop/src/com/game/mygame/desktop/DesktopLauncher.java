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

		TexturePacker.Settings settings = new TexturePacker.Settings();
		settings.paddingX = 0;
		settings.paddingY = 0;
		settings.maxHeight = 512;
		settings.maxWidth = 512;
		TexturePacker.processIfModified(settings, "/home/stefan/libGDX/MyGame/android/assets/player",
				"/home/stefan/libGDX/MyGame/android/assets/player", "anim");
		TexturePacker.processIfModified(settings, "/home/stefan/libGDX/MyGame/android/assets/player_animation",
				"/home/stefan/libGDX/MyGame/android/assets/player_animation", "player_animation");

		new LwjglApplication(new MyGame(), cfg);
	}

}
