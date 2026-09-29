#include "gtg_npc.h"
#include <stddef.h>
#include <stdio.h>
#include <math.h>
_Static_assert(sizeof(GtgNpcObservation)==32, "observation size");
_Static_assert(sizeof(GtgNpcDecision)==16, "decision size");
_Static_assert(offsetof(GtgNpcObservation,health_fraction)==20, "health offset");
_Static_assert(offsetof(GtgNpcDecision,action)==8, "action offset");
#define CHECK(x) do { if (!(x)) { fprintf(stderr,"FAIL line %d: %s\n",__LINE__,#x); return 1; } } while (0)
int main(void) {
 CHECK(gtg_npc_abi_version()==GTG_NPC_ABI_VERSION);
 CHECK(gtg_npc_observation_size()==sizeof(GtgNpcObservation));
 CHECK(gtg_npc_decision_size()==sizeof(GtgNpcDecision));
 GtgNpcObservation in[2]={{.npc_id=7,.role=0,.backend=1,.health_fraction=1,.can_move=0},
                            {.npc_id=8,.role=3,.backend=1,.threat_visible=1,.health_fraction=1,.can_move=1}};
 GtgNpcDecision out[2]={{.npc_id=999,.action=99,.reserved=99},{.npc_id=999,.action=99,.reserved=99}};
 CHECK(gtg_npc_decide_batch(in,2,out,2)==GTG_NPC_OK);
 CHECK(out[0].npc_id==7 && out[0].action==1 && out[0].reserved==0);
 CHECK(out[1].npc_id==8 && out[1].action==3 && out[1].reserved==0);
 CHECK(gtg_npc_decide_batch(NULL,0,NULL,0)==GTG_NPC_OK);
 CHECK(gtg_npc_decide_batch(NULL,1,out,1)==GTG_NPC_NULL);
 CHECK(gtg_npc_decide_batch(in,2,out,1)==GTG_NPC_CAPACITY);
 CHECK(gtg_npc_decide_batch(NULL,4097,NULL,4097)==GTG_NPC_CAPACITY);
 out[0].action=99; out[1].action=99;
 in[1].role=99;
 CHECK(gtg_npc_decide_batch(in,2,out,2)==GTG_NPC_INVALID);
 CHECK(out[0].action==99 && out[1].action==99);
 in[1].role=3; in[1].health_fraction=NAN;
 CHECK(gtg_npc_decide_batch(in,2,out,2)==GTG_NPC_INVALID);
 in[1].health_fraction=1; in[1].reserved=1;
 CHECK(gtg_npc_decide_batch(in,2,out,2)==GTG_NPC_INVALID);
 puts("GTG C ABI smoke test: PASS"); return 0;
}
