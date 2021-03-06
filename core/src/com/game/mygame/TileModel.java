package com.game.mygame;

import com.badlogic.gdx.audio.Sound;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;

public class TileModel extends ModelInstance implements Observer {

	// Remove model from constructor, add game and call getModel... directly

	// The side length of the tile model.
	public static final float SIZE = 2.0f;
	// The delays for reviving the model based on its row and column.
	public static final float ROW_REVIVE_DELAY = 0.2f;
	public static final float COLUMN_REVIVE_DELAY = 0.1f;
	// The speed the model's animations are being played at.
	public static final float REVIVING_SPEED = 2.0f;
	public static final float FALLING_SPEED = 1.3f;

	private enum State {
		ALIVE,
		DYING,
		DEAD,
		REVIVING
	}

	private Tile data;
	private Settings settings;
	public int row, column;
	private State state;
	private AnimationController fallAnimation;
	private BlendAnimation blendAnimation;
	private float reviveDelay;
	private float reviveDelta;
	// Sounds
	private Sound revivingSound;

	public TileModel(Model model, Tile data, int row, int column, MyGame game) {
		super(model);
		this.data = data;
		this.row = row;
		this.column = column;
		settings = game.getSettings();
		revivingSound = game.getSound("Tile-Reviving");
		fallAnimation = new AnimationController(this);
		fallAnimation.allowSameAnimation = true;
		fallAnimation.setAnimation("Cube|Fall");
		blendAnimation = new BlendAnimation(this,
				fallAnimation.current.duration, REVIVING_SPEED);
		reviveDelay = 0.0f;
		reviveDelta = 0.0f;

		// Set living tiles to reviving for setup.
		if (data.isDead())
			state = State.DEAD;
		else
			state = State.REVIVING;
	}

	@Override
	public void updateState(Object... args) {
		if (data.isDead() && state == State.ALIVE) {
			state = State.DYING;
		} else if (!data.isDead()
				&& (state == State.DEAD || state == State.DYING)) {
			state = State.REVIVING;
		}
	}

	public void update(float delta) {
		switch (state) {
			case DEAD:
				break;
			case DYING:
				blendAnimation.update(delta);
				fallAnimation.update(delta);
				if (!blendAnimation.isInAction() && fallAnimation.current.time
						>= fallAnimation.current.duration) {
					state = State.DEAD;
				}
				break;
			case REVIVING:
				reviveDelta += delta;
				if (reviveDelta >= (reviveDelay)) {
					blendAnimation.update(-delta);
					fallAnimation.update(delta);
					if (!blendAnimation.isInAction()
							&& fallAnimation.current.time
							<= fallAnimation.current.duration) {
						if (!settings.getSoundMuted()) {
							revivingSound.play(
									settings.getSoundVolume() / 100f * 0.5f);
						}
						state = State.ALIVE;
						blendAnimation.reset(1.0f, FALLING_SPEED);
						fallAnimation.setAnimation(
								"Cube|Fall", 1, FALLING_SPEED, null);
					}
				}
		}
	}

	public void reset() {
		if (state == State.REVIVING) {
			fallAnimation.setAnimation("Cube|Fall", 1, -REVIVING_SPEED, null);
			blendAnimation.reset(0.0f, REVIVING_SPEED);
		} else {
			fallAnimation.setAnimation("Cube|Fall", 1, FALLING_SPEED, null);
		}
		reviveDelay = 0.0f;
		reviveDelta = 0.0f;
	}

	public int calculateReviveDelay(int firstRowRevived) {
		if (state == State.REVIVING) {
			if (firstRowRevived == -1) {
				reviveDelay = column * COLUMN_REVIVE_DELAY;
				return row;
			} else {
				reviveDelay = (firstRowRevived - row) * ROW_REVIVE_DELAY
						+ column * COLUMN_REVIVE_DELAY;
				return firstRowRevived;
			}
		} else {
			return firstRowRevived;
		}
	}

	public boolean isDead() {
		return (state == State.DEAD);
	}

	public boolean isReviving() {
		return (state == State.REVIVING);
	}

	public float getReviveDelay() {
		return reviveDelay;
	}

}

