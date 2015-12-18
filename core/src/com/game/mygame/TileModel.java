package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;

public class TileModel extends ModelInstance implements Observer {

	// TODO: Delay beim reviven des tiles auf dem der Spieler am anfang steht

	public static float SIZE = 2.0f;

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
	private int reviveDelay;
	private boolean hold;

	public TileModel(Model model, Tile data, int row, int column) {
		super(model);
		//transform.scale(10, 10, 10);
		this.data = data;
		this.row = row;
		this.column = column;
		fallAnimation = new AnimationController(this);
		fallAnimation.allowSameAnimation = true;
		fallAnimation.setAnimation("Cube|Fall", 1, 1.0f, null);
		blendAnimation = new BlendAnimation(this,
				fallAnimation.current.duration);
		reviveDelta = 0.0f;
		reviveDelay = 0;
		hold = false;

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
		} else if (!data.isDead() &&(state == State.DEAD ||
				state == State.DYING)) {
			state = State.REVIVING;
		}
	}

	public void update(float delta) {
		if (hold)
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
				if (reviveDelta >= (reviveDelay - row) + column * 0.5f) {
					blendAnimation.update(-delta);
					fallAnimation.update(delta);
					System.out.println(fallAnimation.current.time + " " + fallAnimation.current.duration + " " + fallAnimation.current.speed);
					if (!blendAnimation.isInAction()) {
						state = State.ALIVE;
						blendAnimation.reset(1.0f);
						fallAnimation.setAnimation("Cube|Fall");
						System.out.println("done setup");
					}
				}
		}
	}

	public void reset() {
		if (state == State.REVIVING) {
			fallAnimation.setAnimation("Cube|Fall", 1, -1.0f, null);
			blendAnimation.reset(0.0f);
		} else {
			fallAnimation.setAnimation("Cube|Fall", 1, 1.0f, null);
		}
		//fallAnimation.update(Gdx.graphics.getDeltaTime());
		reviveDelta = 0.0f;
	}

	public int getFirstRowRevived(int firstRowRevived) {
		if (state == State.REVIVING && firstRowRevived == -1) {
			reviveDelta = 0;
			return row;
		} else {
			reviveDelta = firstRowRevived;
			return firstRowRevived;
		}
	}

	public void hold() {
		hold = true;
	}

	public void release() {
		hold = false;
	}

	public boolean isDead() {
		if (state == State.DEAD)
			return true;
		else
			return false;
	}

}
