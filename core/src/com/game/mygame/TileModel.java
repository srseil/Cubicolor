package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;

public class TileModel extends ModelInstance {

	// TODO: Delay beim reviven des tiles auf dem der Spieler am anfang steht

	public enum TileModelState {
		ALIVE,
		DYING,
		DEAD,
		REVIVING
	}

	private Tile data;
	private int row, column;
	private TileModelState state;
	private AnimationController fallAnimation;
	private BlendAnimation blendAnimation;
	private float reviveDelta;

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

		if (data.isDead())
			state = TileModelState.DEAD;
		else
			state = TileModelState.ALIVE;
	}

	public int update(float delta, int firstRowRevived) {
		switch (state) {
			case DEAD:
				break;
			case DYING:
				blendAnimation.update(delta);
				fallAnimation.update(delta);
				if (!blendAnimation.isInAction())
					state = TileModelState.DEAD;
				break;
			case REVIVING:
				reviveDelta += delta;
				if (firstRowRevived == -1)
					firstRowRevived = row;
				if (reviveDelta >= (firstRowRevived - row) + column * 0.5f) {
					blendAnimation.update(-delta);
					fallAnimation.update(delta);
					if (!blendAnimation.isInAction()) {
						state = TileModelState.ALIVE;
						blendAnimation.reset(1.0f);
						fallAnimation.setAnimation("Cube|Fall");
					}
				}
		}
		return firstRowRevived;
	}

	public void updateState() {
		if (data.isDead() && state == TileModelState.ALIVE) {
			state = TileModelState.DYING;
		} else if (!data.isDead() &&(state == TileModelState.DEAD ||
				state == TileModelState.DYING)) {
			state = TileModelState.REVIVING;
		}
	}

	public void reset() {
		if (state == TileModelState.REVIVING) {
			fallAnimation.setAnimation("Cube|Fall", 1, -1.0f, null);
			blendAnimation.reset(0.0f);
		} else {
			fallAnimation.setAnimation("Cube|Fall", 1, 1.0f, null);
		}
		fallAnimation.update(Gdx.graphics.getDeltaTime());
		reviveDelta = 0.0f;
	}

	public boolean isDead() {
		if (state == TileModelState.DEAD)
			return true;
		else
			return false;
	}

}
