package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.ModelInstance;

public class RequirementModel extends ModelInstance {

	/*
	 * The possible states a RequirementModel can be in.
	 */
	private enum State {
		STILL,
		DESTROYING,
		REVIVING,
		DEAD
	}

	private State state;
	private BlendAnimation blendAnimation;
	private ExitTileModel exitModel;
	private TileColor color;
	private boolean onHold;
	//private boolean alive;

	public RequirementModel(TileColor color, int height, float x, float z,
							ExitTileModel exitModel, MyGame game) {
		super(game.getLockTileModel(color));
		this.color = color;
		this.exitModel = exitModel;
		state = State.DEAD;
		blendAnimation = new BlendAnimation(this, 0.6f);
		blendAnimation.reset(0.0f);
		transform.setTranslation(x, (height+1) * TileModel.SIZE/2, z);
		//alive = true;
	}

	/*
	 * Update the animation of the model based on the time passed since the
	 * last frame.
	 */
	public void update(float delta) {
		if (onHold)
			return;

		switch (state) {
			case STILL: case DEAD:
				// Update particle animation?
				break;
			case DESTROYING:
				blendAnimation.update(delta);
				if (!blendAnimation.isInAction()) {
					state = State.DEAD;
					exitModel.release();
					//alive = false;
				}
				break;
			case REVIVING:
				blendAnimation.update(-delta);
				if (!blendAnimation.isInAction()) {
					state = State.STILL;
					//alive = true;
				}
		}
	}

	/*
	 * Initiate the visual destruction of the model.
	 */
	public void destroy() {
		//exitModel.hold();
		//alive = false;
		blendAnimation.reset(1.0f);
		state = State.DESTROYING;
	}

	/*
	 * Initiate the visual revival of the model.
	 */
	public void revive() {
		blendAnimation.reset(0.0f);
		state = State.REVIVING;
	}

	/*
	 * Hold the model until it manually gets released.
	 */
	public void hold() {
		onHold = true;
	}

	/*
	 * Release the model if it is on hold.
	 */
	public void release() {
		onHold = false;
	}

	public TileColor getColor() {
		return color;
	}

	public boolean isAlive() {
		return (state != State.DEAD);
	}

	public boolean isVisible() {
		return (state != State.DEAD);
	}

}

