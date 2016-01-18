package com.game.mygame;

public class Language {

	public static String getLanguageFilename(String language) {
		switch(language) {
			case "English":
				return "english.xml";
			case "Deutsch":
				return "german.xml";
			default:
				return "english.xml";
		}
	}

}

