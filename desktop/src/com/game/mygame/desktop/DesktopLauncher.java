package com.game.mygame.desktop;

import com.badlogic.gdx.backends.lwjgl.LwjglApplication;
import com.badlogic.gdx.backends.lwjgl.LwjglApplicationConfiguration;
import com.badlogic.gdx.tools.texturepacker.TexturePacker;
import com.game.mygame.MyGame;

public class DesktopLauncher {

	public static void main (String[] arg) {
		LwjglApplicationConfiguration cfg = new LwjglApplicationConfiguration();
		cfg.title = "MyGame";
		// 1440x1080 ist FullHD 4:3
		cfg.width = 800;
		cfg.height = 600;
		cfg.samples = 2;
		//cfg.fullscreen = true;

		cfg.vSyncEnabled = true;
		//cfg.foregroundFPS = 0;
		//cfg.backgroundFPS = 0;

		TexturePacker.Settings settings = new TexturePacker.Settings();
		settings.paddingX = 0;
		settings.paddingY = 0;
		settings.maxHeight = 512;
		settings.maxWidth = 512;

		TexturePacker.processIfModified(settings, "/home/stefan/libGDX/MyGame/android/assets/animations/player_red",
				"/home/stefan/libGDX/MyGame/android/assets/animations/player_red", "player_red");
		TexturePacker.processIfModified(settings, "/home/stefan/libGDX/MyGame/android/assets/animations/player_green",
				"/home/stefan/libGDX/MyGame/android/assets/animations/player_green", "player_green");
		TexturePacker.processIfModified(settings, "/home/stefan/libGDX/MyGame/android/assets/animations/player_blue",
				"/home/stefan/libGDX/MyGame/android/assets/animations/player_blue", "player_blue");
		TexturePacker.processIfModified(settings, "/home/stefan/libGDX/MyGame/android/assets/animations/player_yellow",
				"/home/stefan/libGDX/MyGame/android/assets/animations/player_yellow", "player_yellow");

		TexturePacker.processIfModified(settings, "/home/stefan/libGDX/MyGame/android/assets/textures/keytile",
				"/home/stefan/libGDX/MyGame/android/assets/textures/keytile", "keytile");
		TexturePacker.processIfModified(settings, "/home/stefan/libGDX/MyGame/android/assets/textures/e/",
				"/home/stefan/libGDX/MyGame/android/assets/textures/exittile/", "exittile");

		TexturePacker.processIfModified(settings, "/home/stefan/Bilder/MyGame/skin/ninepatches/",
				"/home/stefan/libGDX/MyGame/android/assets/skins/", "uiskin");

		new LwjglApplication(new MyGame(), cfg);
	}

}
