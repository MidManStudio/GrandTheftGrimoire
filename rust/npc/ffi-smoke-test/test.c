// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-ffi.md, section "test.c"
// ============================================================================
#include "gtg_npc.h"
#include <stddef.h>
#include <stdio.h>
#include <math.h>
_Static_assert(sizeof(GtgNpcObservation)==64, "observation size");
_Static_assert(sizeof(GtgNpcDecision)==16, "decision size");
_Static_assert(offsetof(GtgNpcObservation,npc_id)==0, "npc_id offset");
_Static_assert(offsetof(GtgNpcObservation,role)==8, "role offset");
_Static_assert(offsetof(GtgNpcObservation,backend)==12, "backend offset");
_Static_assert(offsetof(GtgNpcObservation,threat_visible)==16, "threat offset");
_Static_assert(offsetof(GtgNpcObservation,health_fraction)==20, "health offset");
_Static_assert(offsetof(GtgNpcObservation,can_move)==24, "can_move offset");
_Static_assert(offsetof(GtgNpcObservation,threat_distance)==28, "threat_distance offset");
_Static_assert(offsetof(GtgNpcObservation,disposition)==32, "disposition offset");
_Static_assert(offsetof(GtgNpcObservation,order)==33, "order offset");
_Static_assert(offsetof(GtgNpcObservation,provoked)==34, "provoked offset");
_Static_assert(offsetof(GtgNpcObservation,crown_heat)==35, "crown_heat offset");
_Static_assert(offsetof(GtgNpcObservation,level)==36, "level offset");
_Static_assert(offsetof(GtgNpcObservation,order_level)==38, "order_level offset");
_Static_assert(offsetof(GtgNpcObservation,trustworthiness)==40, "trustworthiness offset");
_Static_assert(offsetof(GtgNpcObservation,affinity)==44, "affinity offset");
_Static_assert(offsetof(GtgNpcObservation,pay_satisfaction)==48, "pay_satisfaction offset");
_Static_assert(offsetof(GtgNpcObservation,noise)==52, "noise offset");
_Static_assert(offsetof(GtgNpcObservation,betrayal_opportunity)==56, "betrayal_opportunity offset");
_Static_assert(offsetof(GtgNpcObservation,attack_style)==57, "attack_style offset");
_Static_assert(offsetof(GtgNpcObservation,ranged_ready)==58, "ranged_ready offset");
_Static_assert(offsetof(GtgNpcObservation,ranged_stance)==59, "ranged_stance offset");
_Static_assert(offsetof(GtgNpcObservation,reserved_c)==60, "reserved_c offset");
_Static_assert(offsetof(GtgNpcDecision,action)==8, "action offset");
#define CHECK(x) do { if (!(x)) { fprintf(stderr,"FAIL line %d: %s\n",__LINE__,#x); return 1; } } while (0)

static int decide_one(GtgNpcObservation o, int32_t *action) {
 GtgNpcDecision out[1]={{.npc_id=999,.action=99,.reserved=99}};
 int32_t status=gtg_npc_decide_batch(&o,1,out,1);
 if (status==GTG_NPC_OK) { *action=out[0].action; }
 return status;
}

int main(void) {
 CHECK(gtg_npc_abi_version()==GTG_NPC_ABI_VERSION);
 CHECK(gtg_npc_observation_size()==sizeof(GtgNpcObservation));
 CHECK(gtg_npc_decision_size()==sizeof(GtgNpcDecision));
 GtgNpcObservation in[2]={{.npc_id=7,.role=GTG_NPC_ROLE_MERCHANT,.backend=GTG_NPC_BACKEND_UTILITY,.health_fraction=1,.can_move=0},
                            {.npc_id=8,.role=GTG_NPC_ROLE_ENEMY,.backend=GTG_NPC_BACKEND_UTILITY,.threat_visible=1,.health_fraction=1,.can_move=1}};
 GtgNpcDecision out[2]={{.npc_id=999,.action=99,.reserved=99},{.npc_id=999,.action=99,.reserved=99}};
 CHECK(gtg_npc_decide_batch(in,2,out,2)==GTG_NPC_OK);
 CHECK(out[0].npc_id==7 && out[0].action==GTG_NPC_ACTION_TRADE && out[0].reserved==0);
 CHECK(out[1].npc_id==8 && out[1].action==GTG_NPC_ACTION_ATTACK && out[1].reserved==0);
 CHECK(gtg_npc_decide_batch(NULL,0,NULL,0)==GTG_NPC_OK);
 CHECK(gtg_npc_decide_batch(NULL,1,out,1)==GTG_NPC_NULL);
 CHECK(gtg_npc_decide_batch(in,2,out,1)==GTG_NPC_CAPACITY);
 CHECK(gtg_npc_decide_batch(NULL,4097,NULL,4097)==GTG_NPC_CAPACITY);
 out[0].action=99; out[1].action=99;
 in[1].role=99;
 CHECK(gtg_npc_decide_batch(in,2,out,2)==GTG_NPC_INVALID);
 CHECK(out[0].action==99 && out[1].action==99);
 in[1].role=GTG_NPC_ROLE_ENEMY; in[1].health_fraction=NAN;
 CHECK(gtg_npc_decide_batch(in,2,out,2)==GTG_NPC_INVALID);
 in[1].health_fraction=1; in[1].threat_distance=NAN;
 CHECK(gtg_npc_decide_batch(in,2,out,2)==GTG_NPC_INVALID);
 in[1].threat_distance=INFINITY;
 CHECK(gtg_npc_decide_batch(in,2,out,2)==GTG_NPC_INVALID);
 in[1].threat_distance=0;

 /* Fields added in version 3. A caller that sets only the older fields keeps the old behavior. */
 int32_t action=-1;
 GtgNpcObservation enemy={.npc_id=1,.role=GTG_NPC_ROLE_ENEMY,.backend=GTG_NPC_BACKEND_UTILITY,.threat_visible=1,.health_fraction=1,.can_move=1};
 CHECK(decide_one(enemy,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_ATTACK);
 GtgNpcObservation retaliatory=enemy; retaliatory.disposition=GTG_NPC_DISPOSITION_RETALIATORY;
 CHECK(decide_one(retaliatory,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_PATROL);
 retaliatory.provoked=1;
 CHECK(decide_one(retaliatory,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_ATTACK);
 GtgNpcObservation peaceful=enemy; peaceful.disposition=GTG_NPC_DISPOSITION_PEACEFUL;
 CHECK(decide_one(peaceful,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_RETREAT);

 GtgNpcObservation companion={.npc_id=2,.role=GTG_NPC_ROLE_COMPANION,.backend=GTG_NPC_BACKEND_UTILITY,.health_fraction=1,.can_move=1,.level=10};
 CHECK(decide_one(companion,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_FOLLOW);
 companion.order=GTG_NPC_ORDER_HOLD;
 CHECK(decide_one(companion,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_HOLD);
 companion.trustworthiness=1; companion.affinity=1; companion.pay_satisfaction=1;
 companion.order=GTG_NPC_ORDER_ATTACK; companion.order_level=13;
 CHECK(decide_one(companion,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_FOLLOW);
 companion.order_level=14;
 CHECK(decide_one(companion,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_REFUSE_ORDER);

 /* The refusal gap depends on the order type: level 10 accepts a delivery up to 16, an attack up to 13, a raid up to 11. */
 companion.order=GTG_NPC_ORDER_DELIVER; companion.order_level=16;
 CHECK(decide_one(companion,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_MISSION);
 companion.order_level=17;
 CHECK(decide_one(companion,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_REFUSE_ORDER);
 companion.order=GTG_NPC_ORDER_RAID; companion.order_level=11;
 CHECK(decide_one(companion,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_MISSION);
 companion.order_level=12;
 CHECK(decide_one(companion,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_REFUSE_ORDER);

 /* Betrayal needs an opportunity and low loyalty, and then depends on the roll. */
 GtgNpcObservation traitor={.npc_id=3,.role=GTG_NPC_ROLE_COMPANION,.backend=GTG_NPC_BACKEND_UTILITY,.health_fraction=1,.can_move=1,.level=10,.betrayal_opportunity=1};
 CHECK(decide_one(traitor,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_BETRAY);
 traitor.noise=0xFFFFFFFFu;
 CHECK(decide_one(traitor,&action)==GTG_NPC_OK && action!=GTG_NPC_ACTION_BETRAY);
 traitor.noise=0; traitor.betrayal_opportunity=0;
 CHECK(decide_one(traitor,&action)==GTG_NPC_OK && action!=GTG_NPC_ACTION_BETRAY);

 /* Off-screen mission resolution. */
 CHECK(gtg_npc_resolve_mission(GTG_NPC_ORDER_RAID,10,10,0)==GTG_NPC_MISSION_SUCCESS);
 CHECK(gtg_npc_resolve_mission(GTG_NPC_ORDER_RAID,10,10,0xFFFFFFFFu)==GTG_NPC_MISSION_KILLED);
 CHECK(gtg_npc_resolve_mission(GTG_NPC_ORDER_DELIVER,10,10,0)==GTG_NPC_MISSION_SUCCESS);
 CHECK(gtg_npc_resolve_mission(GTG_NPC_ORDER_ATTACK,10,10,0)==GTG_NPC_INVALID);
 CHECK(gtg_npc_resolve_mission(GTG_NPC_ORDER_RAID,65536,10,0)==GTG_NPC_INVALID);

 /* Crown Heat. Only a retaliatory guard reads it: it fights a visible threat once the tier is 1 or more. */
 GtgNpcObservation guard={.npc_id=4,.role=GTG_NPC_ROLE_GUARD,.backend=GTG_NPC_BACKEND_UTILITY,.threat_visible=1,.health_fraction=1,.can_move=1,.disposition=GTG_NPC_DISPOSITION_RETALIATORY};
 CHECK(decide_one(guard,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_PATROL);
 for (uint8_t tier=1; tier<=GTG_NPC_CROWN_HEAT_MAX; tier++) {
  guard.crown_heat=tier;
  CHECK(decide_one(guard,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_ATTACK);
 }
 GtgNpcObservation hot_enemy=enemy; hot_enemy.disposition=GTG_NPC_DISPOSITION_RETALIATORY; hot_enemy.crown_heat=3;
 CHECK(decide_one(hot_enemy,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_PATROL);
 GtgNpcObservation hot_peaceful=guard; hot_peaceful.disposition=GTG_NPC_DISPOSITION_PEACEFUL;
 CHECK(decide_one(hot_peaceful,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_RETREAT);

 /* Attack styles. Melee is the old behavior whatever the other new fields say. A ranged fighter shoots from far and
    backs away inside 6 m. A hybrid fights by hand inside 4 m, shoots beyond 8 m and keeps its stance in between. */
 GtgNpcObservation archer=enemy; archer.attack_style=GTG_NPC_ATTACK_STYLE_RANGED; archer.ranged_ready=1;
 archer.threat_distance=20;
 CHECK(decide_one(archer,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_SHOOT);
 archer.threat_distance=3;
 CHECK(decide_one(archer,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_KITE);
 archer.threat_distance=-4;
 CHECK(decide_one(archer,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_SHOOT);
 archer.threat_distance=3; archer.ranged_ready=0;
 CHECK(decide_one(archer,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_RETREAT);
 GtgNpcObservation melee_far=enemy; melee_far.threat_distance=20; melee_far.ranged_ready=1; melee_far.ranged_stance=1;
 CHECK(decide_one(melee_far,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_ATTACK);
 GtgNpcObservation knight=enemy; knight.attack_style=GTG_NPC_ATTACK_STYLE_HYBRID; knight.ranged_ready=1;
 knight.threat_distance=2;
 CHECK(decide_one(knight,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_ATTACK);
 knight.threat_distance=12;
 CHECK(decide_one(knight,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_SHOOT);
 knight.threat_distance=6;
 CHECK(decide_one(knight,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_ATTACK);
 knight.ranged_stance=1;
 CHECK(decide_one(knight,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_SHOOT);
 knight.ranged_ready=0;
 CHECK(decide_one(knight,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_ATTACK);

 /* A shopkeeper stands still when a threat is in view. One with the retaliatory disposition that has been attacked
    fights back in its own style. Hostile and peaceful shopkeepers never do. */
 GtgNpcObservation shop={.npc_id=8,.role=GTG_NPC_ROLE_MERCHANT,.backend=GTG_NPC_BACKEND_STATE_MACHINE,.threat_visible=1,.health_fraction=1,.can_move=0,.disposition=GTG_NPC_DISPOSITION_RETALIATORY};
 CHECK(decide_one(shop,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_IDLE);
 shop.provoked=1;
 CHECK(decide_one(shop,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_ATTACK);
 shop.threat_visible=0;
 CHECK(decide_one(shop,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_TRADE);
 shop.threat_visible=1; shop.attack_style=GTG_NPC_ATTACK_STYLE_RANGED; shop.ranged_ready=1; shop.threat_distance=3;
 CHECK(decide_one(shop,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_SHOOT);
 shop.disposition=GTG_NPC_DISPOSITION_HOSTILE;
 CHECK(decide_one(shop,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_IDLE);
 shop.disposition=GTG_NPC_DISPOSITION_PEACEFUL;
 CHECK(decide_one(shop,&action)==GTG_NPC_OK && action==GTG_NPC_ACTION_IDLE);

 /* Anything outside the defined values is rejected, and nothing is written. */
 GtgNpcObservation bad;
 bad=enemy; bad.disposition=3; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=companion; bad.order=6; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=enemy; bad.provoked=2; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=enemy; bad.crown_heat=GTG_NPC_CROWN_HEAT_MAX+1; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=enemy; bad.crown_heat=255; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=enemy; bad.attack_style=3; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=enemy; bad.ranged_ready=2; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=enemy; bad.ranged_stance=2; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=enemy; bad.threat_distance=NAN; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=enemy; bad.reserved_c=1; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=enemy; bad.betrayal_opportunity=2; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=enemy; bad.trustworthiness=NAN; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);
 bad=enemy; bad.role=6; CHECK(decide_one(bad,&action)==GTG_NPC_INVALID);

 /* Overlapping buffers are rejected: the output starts inside the input. */
 GtgNpcObservation shared[4]={enemy,enemy,enemy,enemy};
 CHECK(gtg_npc_decide_batch(shared,2,(GtgNpcDecision *)shared,2)==GTG_NPC_INVALID);
 puts("GTG C ABI smoke test: PASS"); return 0;
}
