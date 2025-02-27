<template>
  <form class="form-horizontal" hide-footer>
    <b-modal id="proposeTradeForm" ref="proposeTradeFormRef" size="lg" title="Propose Trade" @hidden="clearData">
      <div class="form-group">
        <label for="counterParty" class="control-label">Publisher to trade with</label>
        <b-form-select v-model="counterParty">
          <option v-for="publisher in otherPublishers" :key="publisher.publisherID" :value="publisher">
            {{ publisher.publisherName }}
          </option>
        </b-form-select>
      </div>

      <div v-if="counterParty">
        <div class="row">
          <div class="col-6">
            <h4 class="text-black">Offer</h4>

            <div>
              <div v-for="(item, index) in proposerPublisherGames" :key="item.publisherGameID">
                <div class="trade-game-row">
                  <label>{{ index + 1 }}</label>
                  <b-form-select v-model="item.game">
                    <option v-for="publisherGame in userPublisher.games" :key="publisherGame.publisherGameID" :value="publisherGame">
                      {{ getGameOptionName(publisherGame) }}
                    </option>
                  </b-form-select>

                  <div class="close-button fake-link" @click="removeProposerGame(item.id)">
                    <font-awesome-icon icon="times" size="lg" :style="{ color: '#414141' }" />
                  </div>
                </div>
              </div>
              <b-button variant="secondary" class="full-width-button" @click="addGame(proposerPublisherGames)">Add Game</b-button>
            </div>
          </div>
          <div class="col-6">
            <h4 class="text-black">Receive</h4>

            <div>
              <div v-for="(item, index) in counterPartyPublisherGames" :key="item.publisherGameID">
                <div class="trade-game-row">
                  <label>{{ index + 1 }}</label>
                  <b-form-select v-model="item.game">
                    <option v-for="publisherGame in counterParty.games" :key="publisherGame.publisherGameID" :value="publisherGame">
                      {{ getGameOptionName(publisherGame) }}
                    </option>
                  </b-form-select>

                  <div class="close-button fake-link" @click="removeCounterPartyGame(item.id)">
                    <font-awesome-icon icon="times" size="lg" :style="{ color: '#414141' }" />
                  </div>
                </div>
              </div>
              <b-button variant="secondary" class="full-width-button" @click="addGame(counterPartyPublisherGames)">Add Game</b-button>
            </div>
          </div>
        </div>
        <div class="row">
          <div class="col-6">
            <label>Budget (Current Budget: ${{ userPublisher.budget }})</label>
            <input id="proposerBudgetSendAmount" v-model="proposerBudgetSendAmount" name="proposerBudgetSendAmount" type="number" class="form-control input" />
          </div>
          <div class="col-6">
            <label>Budget (Current Budget: ${{ counterParty.budget }})</label>
            <input id="counterPartyBudgetSendAmount" v-model="counterPartyBudgetSendAmount" name="counterPartyBudgetSendAmount" type="number" class="form-control input" />
          </div>
        </div>

        <div class="form-group">
          <label for="messageText" class="control-label">Message (All players will see this message.)</label>
          <textarea v-model="message" class="form-control" rows="3"></textarea>
        </div>
      </div>

      <div v-show="clientError" class="alert alert-warning">{{ clientError }}</div>
      <div v-show="serverError" class="alert alert-danger">{{ serverError }}</div>

      <template #modal-footer>
        <input v-show="counterParty" type="submit" class="btn btn-primary" value="Propose Trade" :disabled="isBusy" @click="proposeTrade" />
      </template>
    </b-modal>
  </form>
</template>
<script>
import axios from 'axios';

import LeagueMixin from '@/mixins/leagueMixin.js';

export default {
  mixins: [LeagueMixin],
  data() {
    return {
      counterParty: null,
      proposerPublisherGames: [],
      counterPartyPublisherGames: [],
      proposerBudgetSendAmount: 0,
      counterPartyBudgetSendAmount: 0,
      message: '',
      indexer: 0,
      clientError: '',
      serverError: '',
      isBusy: false
    };
  },
  computed: {
    getTradeError() {
      if (this.proposerPublisherGames.length === 0 && this.counterPartyPublisherGames.length === 0) {
        return 'A trade must involve at least one game.';
      }

      let allGames = this.proposerPublisherGames.concat(this.counterPartyPublisherGames);
      let nullGames = allGames.some((x) => !x.game);
      if (nullGames) {
        return 'All games must be defined.';
      }

      if (this.proposerPublisherGames.length === 0 && !this.proposerBudgetSendAmount) {
        return 'You must offer something.';
      }

      if (this.counterPartyPublisherGames.length === 0 && !this.counterPartyBudgetSendAmount) {
        return 'You must receive something.';
      }

      if (this.proposerBudgetSendAmount > 0 && this.counterPartyBudgetSendAmount > 0) {
        return 'You cannot have budget on both sides of the trade.';
      }

      if (!this.message) {
        return 'You must include a message.';
      }

      return '';
    },
    otherPublishers() {
      return this.leagueYear.publishers.filter((x) => x.publisherID !== this.userPublisher.publisherID);
    }
  },
  methods: {
    addGame(list) {
      const element = {
        id: this.indexer++,
        game: null
      };
      list.push(element);
    },
    removeProposerGame(id) {
      this.proposerPublisherGames = this.proposerPublisherGames.filter((x) => x.id !== id);
    },
    removeCounterPartyGame(id) {
      this.counterPartyPublisherGames = this.counterPartyPublisherGames.filter((x) => x.id !== id);
    },
    getGameOptionName(game) {
      if (game.counterPick) {
        return `${game.gameName} (Counter Pick)`;
      }

      return game.gameName;
    },
    async proposeTrade() {
      this.clientError = this.getTradeError;
      if (this.clientError) {
        return;
      }

      const request = {
        proposerPublisherID: this.userPublisher.publisherID,
        counterPartyPublisherID: this.counterParty.publisherID,
        proposerPublisherGameIDs: this.proposerPublisherGames.map((x) => x.game.publisherGameID),
        counterPartyPublisherGameIDs: this.counterPartyPublisherGames.map((x) => x.game.publisherGameID),
        proposerBudgetSendAmount: this.proposerBudgetSendAmount,
        counterPartyBudgetSendAmount: this.counterPartyBudgetSendAmount,
        message: this.message
      };

      this.isBusy = true;
      try {
        await axios.post('/api/league/ProposeTrade', request);
        this.$refs.proposeTradeFormRef.hide();
        this.notifyAction('You proposed a trade.');
        this.clearData();
      } catch (error) {
        this.serverError = error.response.data;
      } finally {
        this.isBusy = false;
      }
    },
    clearData() {
      this.counterParty = null;
      this.proposerPublisherGames = [];
      this.counterPartyPublisherGames = [];
      this.proposerBudgetSendAmount = 0;
      this.counterPartyBudgetSendAmount = 0;
      this.message = '';
    }
  }
};
</script>
<style scoped>
.trade-game-row {
  width: 100%;
  display: inline-flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  margin-bottom: 5px;
}
</style>
