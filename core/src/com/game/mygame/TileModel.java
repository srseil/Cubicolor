package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;
import com.badlogic.gdx.utils.Timer;

public class TileModel extends ModelInstance implements Observer {

	public static final float SIZE = 2.0f;
	public static final float ROW_REVIVE_DELAY = 0.3f;
	public static final float COLUMN_REVIVE_DELAY = 0.15f;

	private enum State {
		ALIVE,
		DYING,
		DEAD,
		REVIVING
	}

	private Tile data;
	private int row, column;
	private State state;
	private AnimationController fallAnimation;
	private BlendAnimation blendAnimation;
	private float reviveDelta;
	private float reviveDelay;
	private boolean onHold;

	public TileModel(Model model, Tile data, int row, int column) {
		super(model);
		this.data = data;
		this.row = row;
		this.column = column;
		fallAnimation = new AnimationController(this);
		fallAnimation.allowSameAnimation = true;
		fallAnimation.setAnimation("Cube|Fall");
		blendAnimation = new BlendAnimation(this,
				fallAnimation.current.duration);
		reviveDelta = 0.0f;
		reviveDelay = 0;
		//hold = false;

		// Set living tiles to reviving for setup.
		if (data.isDead())
			state = State.DEAD;
		else
			state = State.REVIVING;
	}

	@Override
	public void updateState() {
		if (data.isDead() && state == State.ALIVE) {
			state = State.DYING;
		} else if (!data.isDead() && (state == State.DEAD ||
				state == State.DYING)) {
			state = State.REVIVING;
		}
	}

	public void update(float delta) {
		if (onHold)
			return;

		switch (state) {
			case DEAD:
				break;
			case DYING:
				blendAnimation.update(delta);
				fallAnimation.update(delta);
				if (!blendAnimation.isInAction())
					state = State.DEAD;
				break;
			case REVIVING:
				reviveDelta += delta;
				if (reviveDelta >= (reviveDelay)) {
					System.out.println("DELAY: " + reviveDelay);
				//if (reviveDelta >= (reviveDelay) + column * 0.5f) {
				//if (reviveDelta >= (row * 0.5f) + column * 0.5f) {
					if (row == 1 && (column == 2 || column == 3))
						System.out.println("TILE: " + row + " " + column + " " + System.currentTimeMillis());
					blendAnimation.update(-delta);
					fallAnimation.update(delta);
					//System.out.println(fallAnimation.current.time + " " + fallAnimation.current.duration + " " + fallAnimation.current.speed);
					if (!blendAnimation.isInAction()) {
						state = State.ALIVE;
						blendAnimation.reset(1.0f);
						fallAnimation.setAnimation("Cube|Fall");
					}
				}
		}
	}

	public void reset() {
		if (state == State.REVIVING) {
			fallAnimation.setAnimation("Cube|Fall", 1, -2.0f, null);
			blendAnimation.reset(0.0f);
		} else {
			fallAnimation.setAnimation("Cube|Fall", 1, 1.0f, null);
		}
		//fallAnimation.update(Gdx.graphics.getDeltaTime());
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

	public void hold() {
		onHold = true;
	}

	public void release() {
		onHold = false;
	}

	public boolean isDead() {
		if (state == State.DEAD)
			return true;
		else
			return false;
	}

}
