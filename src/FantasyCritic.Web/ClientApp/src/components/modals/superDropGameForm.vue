<template>
  <b-modal id="superDropGameForm" ref="superDropGameFormRef" size="lg" title="Drop a Game" hide-footer @hidden="clearData">
    <div v-if="errorInfo" class="alert alert-danger" role="alert">
      {{ errorInfo }}
    </div>
    <p>You can use this form to request to super drop a game.</p>
    <form class="form-horizontal" hide-footer @submit.prevent="dropGame">
      <div class="form-group">
        <label for="gameToDrop" class="control-label">Game</label>
        <b-form-select v-model="gameToDrop">
          <option v-for="publisherGame in droppableGames" :key="publisherGame.publisherGameID" :value="publisherGame">
            {{ publisherGame.gameName }}
          </option>
        </b-form-select>
      </div>

      <div v-if="gameToDrop">
        <input type="submit" class="btn btn-danger add-game-button" value="Super Drop Game" :disabled="isBusy" />
      </div>
      <hr />
      <div v-if="dropResult && !dropResult.success" class="alert bid-error alert-danger">
        <h3 class="alert-heading">Error!</h3>
        <ul>
          <li v-for="error in dropResult.errors" :key="error">{{ error }}</li>
        </ul>
      </div>
    </form>
  </b-modal>
</template>

<script>
import axios from 'axios';
import LeagueMixin from '@/mixins/leagueMixin';

export default {
  mixins: [LeagueMixin],
  data() {
    return {
      dropResult: null,
      gameToDrop: null,
      isBusy: false,
      errorInfo: ''
    };
  },
  computed: {
    formIsValid() {
      return this.dropMasterGame;
    },
    droppableGames() {
      return _.filter(this.userPublisher.games, { counterPick: false });
    }
  },
  methods: {
    dropGame() {
      var request = {
        publisherID: this.userPublisher.publisherID,
        publisherGameID: this.gameToDrop.publisherGameID
      };
      this.isBusy = true;
      axios
        .post('/api/league/MakeDropRequest', request)
        .then((response) => {
          this.isBusy = false;
          this.dropResult = response.data;
          if (!this.dropResult.success) {
            return;
          }

          this.notifyAction('Drop Request for ' + this.gameToDrop.gameName + ' was made.');
          this.$refs.superDropGameFormRef.hide();
          this.clearData();
        })
        .catch((response) => {
          this.isBusy = false;
          this.errorInfo = response.response.data;
        });
    },
    clearData() {
      this.dropResult = null;
      this.gameToDrop = null;
    }
  }
};
</script>